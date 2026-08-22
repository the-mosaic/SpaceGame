using Serilog;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System.Runtime.InteropServices;

namespace SpaceGame.Engine.Render;

public unsafe class Renderer
{
    // vulkan related variables
    public Vk vk { get; private set; }
    public Instance Instance { get; private set; }
    public VulkanDebug Debug { get; private set; }
    public VulkanDevice Device { get; private set; }
    private VulkanSwapchain _swapchain;
    private KhrSurface _khrSurfaceExt;
    public KhrSurface KhrSurfaceExt { get => _khrSurfaceExt; }
    public SurfaceKHR Surface { get; private set; }
    
    private IWindow _window;
    private readonly ILogger _log;
    public bool WantValidationLayers { get; private set; }

    public Renderer(bool wantValidationLayers, IWindow window)
    {
        _window = window;
        WantValidationLayers = wantValidationLayers;

        _log = SpaceGame.Logger!.ForContext<Renderer>();
        vk = Vk.GetApi();

        // Vulkan Initialization

        // Create the debug manager.
        // This has to be done BEFORE CreateInstance() because it depends on
        // non-static methods of DebugManager
        Debug = new VulkanDebug(this);

        // Create the instance.
        Instance = CreateInstance();

        // Now actually set up our debug messsenger. This function needs an
        // instance to be created for it to work.
        if (WantValidationLayers) Debug.SetupDebugger();

        // Set up our surface extension...
        if (!vk.TryGetInstanceExtension(Instance, out _khrSurfaceExt))
            throw new Exception("KHR_surface extension not found, can't render anything");
        
        Surface = window.VkSurface!.Create<AllocationCallbacks>(Instance.ToHandle(), null).ToSurface();

        // Create our device. This little wrapper class will handle everything
        // for us just by using new().
        Device = new VulkanDevice(this);

        // Logging for debug stuff
        if (WantValidationLayers)
            _log.Information("Selected physical device {Name}, API Version {ApiVersion}", Device.DeviceName, Device.ApiVersionStr);
        
        // Now create the swapchain
        _swapchain = new VulkanSwapchain(this, SpaceGame.Instance!.SpWindow);
    }

    // INITIALIZATION FUNCTIONS
    private Instance CreateInstance()
    {
        if (WantValidationLayers && !Debug.ValidationLayersSupported())
        {
            // Should we exit here or just do a warning?
            _log.Warning("Validation layers requested, but the system does not support the required layers. Continuing without validation.");
            WantValidationLayers = false;
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
        if (WantValidationLayers)
            Debug.AddValidationLayers(ref instanceInfo, &debugCreateInfo);

        if (vk.CreateInstance(in instanceInfo, null, out var result) != Result.Success)
            throw new Exception("Vulkan creation failed");

        // grumble grumble
        Marshal.FreeHGlobal((nint)appInfo.PApplicationName);
        Marshal.FreeHGlobal((nint)appInfo.PEngineName);
        SilkMarshal.Free((nint)instanceInfo.PpEnabledExtensionNames);

        if (WantValidationLayers)
            SilkMarshal.Free((nint)instanceInfo.PpEnabledLayerNames);
        
        return result;
    }

    private string[] GetRequiredExtensions()
    {
        var window = SpaceGame.Instance!.SpWindow!;
        var glfwExntensions = window.VkSurface!.GetRequiredExtensions(out var glfwExtensionCount);
        var extensions = SilkMarshal.PtrToStringArray((IntPtr)glfwExntensions, (int)glfwExtensionCount);

        if (WantValidationLayers)
            extensions = [..extensions, ExtDebugUtils.ExtensionName];

        return extensions;
    }

    // RENDERING FUNCTIONS

    // CLEANUP FUNCTIONS
    public void Destroy()
    {
        _swapchain.Destroy();
        Device.Destroy();

        KhrSurfaceExt.DestroySurface(Instance, Surface, null);
        
        Debug.Destroy();
        vk.DestroyInstance(Instance, null);
        vk.Dispose();
    }
}
