using Serilog;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System.Runtime.InteropServices;

namespace SpaceGame.Engine;

struct QueueFamilyIndicies
{
    public uint? GraphicsFamily { get; set; }
    public uint? PresentFamily { get; set; }

    public readonly bool IsComplete()
    {
        return GraphicsFamily.HasValue && PresentFamily.HasValue;
    }
}

public unsafe class Renderer
{
    // vulkan related variables
    public Vk vk { get; private set; }
    public Instance Instance { get; private set; }
    private readonly RenderDebug _debugMgr;

    // device
    private PhysicalDevice _physicalDevice;
    private PhysicalDeviceProperties _physicalDeviceProperties;
    private QueueFamilyIndicies _queueFamilyIndicies;
    private Device _device;
    private KhrSurface _khrSurfaceExt;
    private SurfaceKHR _surface;

    // queues
    private Queue _graphicsQueue;

    private readonly ILogger _log;
    private bool _wantValidationLayers;

    public Renderer(bool wantValidationLayers, IWindow window)
    {
        _wantValidationLayers = wantValidationLayers;

        _log = SpaceGame.Logger!.ForContext<Renderer>();
        vk = Vk.GetApi();

        // Vulkan Initialization

        // Create the debug manager.
        // This has to be done BEFORE CreateInstance() because it depends on
        // non-static methods of DebugManager
        _debugMgr = new RenderDebug(this);

        // Create the instance.
        Instance = CreateInstance();

        // Now actually set up our debug messsenger. This function needs an
        // instance to be created for it to work.
        if (_wantValidationLayers) _debugMgr.SetupDebugger();

        // Set up our surface extension...
        if (!vk.TryGetInstanceExtension(Instance, out _khrSurfaceExt))
            throw new Exception("KHR_surface extension not found, can't render anything");
        
        _surface = window.VkSurface!.Create<AllocationCallbacks>(Instance.ToHandle(), null).ToSurface();

        // Find a physical device. If none that support the features we need is
        // found, this function throws an exception.
        _physicalDevice = FindPhysicalDevice();
        _queueFamilyIndicies = GetQueueFamilyIndicies(_physicalDevice);
        vk.GetPhysicalDeviceProperties(_physicalDevice, out _physicalDeviceProperties);

        // Logging for debug stuff
        if (_wantValidationLayers)
        {
            fixed(byte* deviceName = _physicalDeviceProperties.DeviceName)
            {
                _log.Information(
                    "Selected physical device {Name}, API Version {ApiVersion}",
                    SilkMarshal.PtrToString((nint)deviceName),
                    RenderUtil.VersionToString(_physicalDeviceProperties.ApiVersion)
                );
            }
        }

        // Now make our logical device.
        _device = CreateLogicalDevice();
        vk.GetDeviceQueue(_device, _queueFamilyIndicies.GraphicsFamily!.Value, 0, out _graphicsQueue);
    }

    // INITIALIZATION FUNCTIONS
    private Instance CreateInstance()
    {
        if (_wantValidationLayers && !_debugMgr.ValidationLayersSupported())
        {
            // Should we exit here or just do a warning?
            _log.Warning("Validation layers requested, but the system does not support the required layers. Continuing without validation.");
            _wantValidationLayers = false;
        }

        // I really don't like having to use Marshal stuff here, but apparently
        // Silk.NET only supports using Spans for their OpenGL APIs(!!), so I'm
        // stuck with this ugly code for now.
        var appInfo = new ApplicationInfo()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Space game"),
            ApplicationVersion = SpaceGame.ApplicationVersion,
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi("No Engine"),
            EngineVersion = SpaceGame.ApplicationVersion,
            ApiVersion = Vk.Version12
        };

        var extensions = GetRequiredExtensions();
        var instanceInfo = new InstanceCreateInfo()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
            EnabledExtensionCount = (uint)extensions.Length,
            PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions),
            EnabledLayerCount = 0,
            PNext = null
        };

        DebugUtilsMessengerCreateInfoEXT debugCreateInfo = default;
        if (_wantValidationLayers)
            _debugMgr.AddValidationLayers(ref instanceInfo, &debugCreateInfo);

        if (vk.CreateInstance(in instanceInfo, null, out var result) != Result.Success)
            throw new Exception("Vulkan creation failed");

        // grumble grumble
        Marshal.FreeHGlobal((nint)appInfo.PApplicationName);
        Marshal.FreeHGlobal((nint)appInfo.PEngineName);
        SilkMarshal.Free((nint)instanceInfo.PpEnabledExtensionNames);

        if (_wantValidationLayers)
            SilkMarshal.Free((nint)instanceInfo.PpEnabledLayerNames);
        
        return result;
    }

    private string[] GetRequiredExtensions()
    {
        var window = SpaceGame.Instance!.SpWindow!;
        var glfwExntensions = window.VkSurface!.GetRequiredExtensions(out var glfwExtensionCount);
        var extensions = SilkMarshal.PtrToStringArray((IntPtr)glfwExntensions, (int)glfwExtensionCount);

        if (_wantValidationLayers)
            extensions = [..extensions, ExtDebugUtils.ExtensionName];

        return extensions;
    }

    private PhysicalDevice FindPhysicalDevice()
    {
        var devices = vk!.GetPhysicalDevices(Instance);

        foreach (var device in devices)
        {
            var familyIndicies = GetQueueFamilyIndicies(device);

            if (familyIndicies.IsComplete())
                return device;
        }

        throw new Exception("No supported physical device found!");
    }

    private QueueFamilyIndicies GetQueueFamilyIndicies(PhysicalDevice physDevice)
    {
        var indicies = new QueueFamilyIndicies();
        var queueFamilyCount = 0u;

        vk.GetPhysicalDeviceQueueFamilyProperties(physDevice, ref queueFamilyCount, null);

        var families = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* familiesPointer = families)
            vk.GetPhysicalDeviceQueueFamilyProperties(physDevice, ref queueFamilyCount, familiesPointer);

        for (var i = 0u; i < families.Length; i++)
        {
            var family = families[i];

            if (family.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
                indicies.GraphicsFamily = i;
            
            _khrSurfaceExt.GetPhysicalDeviceSurfaceSupport(physDevice, i, _surface, out var presentSupport);
            if (presentSupport)
                indicies.PresentFamily = i;

            if (indicies.IsComplete()) break;
        }

        return indicies;
    }

    private Device CreateLogicalDevice()
    {
        var indicies = GetQueueFamilyIndicies(_physicalDevice);
        var queuePriority = 1.0f;

        var queueCreateInfo = new DeviceQueueCreateInfo()
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = indicies.GraphicsFamily!.Value,
            QueueCount = 1,
            PQueuePriorities = &queuePriority
        };

        var deviceFeatures = new PhysicalDeviceFeatures();
        var deviceCreateInfo = new DeviceCreateInfo()
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = 1,
            PQueueCreateInfos = &queueCreateInfo,
            PEnabledFeatures = &deviceFeatures,
            EnabledExtensionCount = 0,
            EnabledLayerCount = 0
        };

        if (_wantValidationLayers)
            _debugMgr.AddValidationLayers(ref deviceCreateInfo);

        if (vk.CreateDevice(_physicalDevice, in deviceCreateInfo, null, out var device) != Result.Success)
            throw new Exception("Logical device creation failed!");

        if (_wantValidationLayers)
            SilkMarshal.Free((nint)deviceCreateInfo.PpEnabledLayerNames);
        
        return device;
    }

    // RENDERING FUNCTIONS

    // CLEANUP FUNCTIONS
    public void Destroy()
    {
        vk.DestroyDevice(_device, null);
        _debugMgr.Destroy();

        _khrSurfaceExt.DestroySurface(Instance, _surface, null);
        vk.DestroyInstance(Instance, null);
        vk.Dispose();
    }
}
