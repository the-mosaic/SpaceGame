using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace SpaceGame.Engine.Render.Vulkan;

public struct QueueFamilyIndicies
{
    public uint? GraphicsFamily { get; set; }
    public uint? PresentFamily { get; set; }

    public readonly bool IsComplete()
    {
        return GraphicsFamily.HasValue && PresentFamily.HasValue;
    }
}

public struct SwapchainSupportDetails
{
    public SurfaceCapabilitiesKHR Capabilities { get; set; }
    public SurfaceFormatKHR[] Formats { get; set; }
    public PresentModeKHR[] PresentModes { get; set; }
}

// wrapper class for physical and logical devices
public unsafe class VulkanDevice
{
    public static readonly string[] DeviceExtensions = [KhrSwapchain.ExtensionName];

    public PhysicalDevice PhysicalDevice { get; private set; }
    public Device LogicalDevice { get; private set; }
    public SwapchainSupportDetails SwapchainSupport { get; private set; }
    private PhysicalDeviceProperties _physicalDeviceProperties;
    private QueueFamilyIndicies _queueFamilyIndicies;
    private readonly VulkanContext _context;

    // queues
    private Queue _graphicsQueue;
    private Queue _presentQueue;

    public Queue GraphicsQueue { get => _graphicsQueue; }
    public Queue PresentQueue { get => _presentQueue; }

    public VulkanDevice(VulkanContext context)
    {
        _context = context;

        // Find a physical device. If none that support the features we need is
        // found, this function throws an exception.
        PhysicalDevice = FindPhysicalDevice();
        SwapchainSupport = GetSwapchainSupport(PhysicalDevice);
        _queueFamilyIndicies = GetQueueFamilyIndicies(PhysicalDevice);
        _context.Vk.GetPhysicalDeviceProperties(PhysicalDevice, out _physicalDeviceProperties);

        // Now make our logical device.
        LogicalDevice = CreateLogicalDevice();
        _context.Vk.GetDeviceQueue(LogicalDevice, _queueFamilyIndicies.GraphicsFamily!.Value, 0, out _graphicsQueue);
        _context.Vk.GetDeviceQueue(LogicalDevice, _queueFamilyIndicies.PresentFamily!.Value, 0, out _presentQueue);
    }

    private PhysicalDevice FindPhysicalDevice()
    {
        var devices = _context.Vk.GetPhysicalDevices(_context.Instance);

        foreach (var device in devices)
        {
            var familyIndicies = GetQueueFamilyIndicies(device);
            var swapchainsSupported = false;
            var extensionsSupported = DeviceSupportsExtensions(device);

            if (extensionsSupported)
            {
                var swapchainDetails = GetSwapchainSupport(device);
                swapchainsSupported = swapchainDetails.Formats.Length != 0 && swapchainDetails.PresentModes.Length != 0;
            }

            if (familyIndicies.IsComplete() && extensionsSupported && swapchainsSupported)
                return device;
        }

        throw new Exception("No supported physical device found!");
    }

    private QueueFamilyIndicies GetQueueFamilyIndicies(PhysicalDevice physDevice)
    {
        if (!_context.Surface.HasValue || _context.ExtSurface is null)
            throw new Exception("Attempted to call GetQueueFamilyIndicies() before surface creation!");

        var indicies = new QueueFamilyIndicies();
        var queueFamilyCount = 0u;

        _context.Vk.GetPhysicalDeviceQueueFamilyProperties(physDevice, ref queueFamilyCount, null);

        var families = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* familiesPointer = families)
            _context.Vk.GetPhysicalDeviceQueueFamilyProperties(physDevice, ref queueFamilyCount, familiesPointer);

        for (var i = 0u; i < families.Length; i++)
        {
            var family = families[i];

            if (family.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
                indicies.GraphicsFamily = i;
            
            _context.ExtSurface.GetPhysicalDeviceSurfaceSupport(physDevice, i, _context.Surface.Value, out var presentSupport);
            if (presentSupport)
                indicies.PresentFamily = i;

            if (indicies.IsComplete()) break;
        }

        return indicies;
    }

    public QueueFamilyIndicies GetQueueFamilyIndicies() => GetQueueFamilyIndicies(PhysicalDevice);

    private bool DeviceSupportsExtensions(PhysicalDevice physDevice)
    {
        var extensionCount = 0u;
        _context.Vk.EnumerateDeviceExtensionProperties(physDevice, (byte*)null, ref extensionCount, null);

        var availableExtensions = new ExtensionProperties[extensionCount];
        fixed(ExtensionProperties* ptr = availableExtensions)
            _context.Vk.EnumerateDeviceExtensionProperties(physDevice, (byte*)null, ref extensionCount, ptr);
        
        var availableExtensionNames = availableExtensions.Select(extension => Marshal.PtrToStringAnsi((nint)extension.ExtensionName)).ToHashSet();
        return DeviceExtensions.All(availableExtensionNames.Contains);
    }

    private SwapchainSupportDetails GetSwapchainSupport(PhysicalDevice physDevice)
    {
        if (!_context.Surface.HasValue || _context.ExtSurface is null)
            throw new Exception("Attempted to call GetSwapchainSupport() before surface creation!");
        
        var ExtSurface = _context.ExtSurface;

        // Query our capabilities...
        ExtSurface.GetPhysicalDeviceSurfaceCapabilities(physDevice, _context.Surface.Value, out var capabilities);
        
        // ...the formats that the device supports...
        var formatCount = 0u;
        ExtSurface.GetPhysicalDeviceSurfaceFormats(physDevice, _context.Surface.Value, ref formatCount, null);

        var formats = new SurfaceFormatKHR[formatCount];
        fixed (SurfaceFormatKHR* ptr = formats)
            ExtSurface.GetPhysicalDeviceSurfaceFormats(physDevice, _context.Surface.Value, ref formatCount, ptr);

        // ...and the present modes.
        var presentModeCount = 0u;
        ExtSurface.GetPhysicalDeviceSurfacePresentModes(physDevice, _context.Surface.Value, ref presentModeCount, null);
        
        var presentModes = new PresentModeKHR[presentModeCount];
        fixed (PresentModeKHR* ptr = presentModes)
            ExtSurface.GetPhysicalDeviceSurfacePresentModes(physDevice, _context.Surface.Value, ref presentModeCount, ptr);

        return new SwapchainSupportDetails()
        {
            Capabilities = capabilities,
            Formats = formats,
            PresentModes = presentModes
        };
    }

    public SwapchainSupportDetails GetSwapchainSupport() => GetSwapchainSupport(PhysicalDevice);

    private Device CreateLogicalDevice()
    {
        var indicies = GetQueueFamilyIndicies(PhysicalDevice);
        var queuePriority = 1f;

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
            EnabledExtensionCount = (uint)DeviceExtensions.Length,
            PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(DeviceExtensions),
            EnabledLayerCount = 0
        };

        if (_context.WantValidationLayers)
            VulkanDebug.AddValidationLayers(ref deviceCreateInfo);

        if (_context.Vk.CreateDevice(PhysicalDevice, in deviceCreateInfo, null, out var device) != Result.Success)
            throw new Exception("Logical device creation failed!");

        if (_context.WantValidationLayers)
            SilkMarshal.Free((nint)deviceCreateInfo.PpEnabledLayerNames);
        
        return device;
    }
    
    public ShaderModule CreateShaderModule(ShaderModuleCreateInfo createInfo)
    {
        if (_context.Vk.CreateShaderModule(LogicalDevice, in createInfo, null, out var result) != Result.Success)
            throw new Exception("Shader module creation failed!");
        
        return result;
    }

    public void DestroyShaderModule(ShaderModule module)
    {
        _context.Vk.DestroyShaderModule(LogicalDevice, module, null);
    }

    public void Destroy()
    {
        _context.Vk.DestroyDevice(LogicalDevice, null);
    }

    public static string VersionToString(uint version)
    {
        uint major = version >> 22;
        uint minor = (version >> 12) & 0x3FF;
        uint patch = version & 0xFFF;

        return $"{major}.{minor}.{patch}";
    }

    // synthetic fields
    public string? DeviceName
    {
        get
        {
            fixed(byte* deviceName = _physicalDeviceProperties.DeviceName)
            {
                return SilkMarshal.PtrToString((nint)deviceName);
            }
        }
    }

    public string? ApiVersionStr
    {
        get
        {
            return VersionToString(_physicalDeviceProperties.ApiVersion);
        }
    }
}