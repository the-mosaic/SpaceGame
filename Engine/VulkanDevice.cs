using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

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

// wrapper class for physical and logical devices
public unsafe class VulkanDevice
{
    public PhysicalDevice PhysicalDevice { get; private set; }
    public Device LogicalDevice { get; private set; }
    private PhysicalDeviceProperties _physicalDeviceProperties;
    private QueueFamilyIndicies _queueFamilyIndicies;
    private readonly Renderer _renderer;

    // queues
    private Queue _graphicsQueue;
    private Queue _presentQueue;

    public Queue GraphicsQueue { get => _graphicsQueue; }
    public Queue PresentQueue { get => _presentQueue; }

    public VulkanDevice(Renderer renderer)
    {
        _renderer = renderer;

        // Find a physical device. If none that support the features we need is
        // found, this function throws an exception.
        PhysicalDevice = FindPhysicalDevice();
        _queueFamilyIndicies = GetQueueFamilyIndicies(PhysicalDevice);
        _renderer.vk.GetPhysicalDeviceProperties(PhysicalDevice, out _physicalDeviceProperties);

        // Now make our logical device.
        LogicalDevice = CreateLogicalDevice();
        _renderer.vk.GetDeviceQueue(LogicalDevice, _queueFamilyIndicies.GraphicsFamily!.Value, 0, out _graphicsQueue);
        _renderer.vk.GetDeviceQueue(LogicalDevice, _queueFamilyIndicies.PresentFamily!.Value, 0, out _presentQueue);
    }

    private PhysicalDevice FindPhysicalDevice()
    {
        var devices = _renderer.vk.GetPhysicalDevices(_renderer.Instance);

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

        _renderer.vk.GetPhysicalDeviceQueueFamilyProperties(physDevice, ref queueFamilyCount, null);

        var families = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* familiesPointer = families)
            _renderer.vk.GetPhysicalDeviceQueueFamilyProperties(physDevice, ref queueFamilyCount, familiesPointer);

        for (var i = 0u; i < families.Length; i++)
        {
            var family = families[i];

            if (family.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
                indicies.GraphicsFamily = i;
            
            _renderer.KhrSurfaceExt.GetPhysicalDeviceSurfaceSupport(physDevice, i, _renderer.Surface, out var presentSupport);
            if (presentSupport)
                indicies.PresentFamily = i;

            if (indicies.IsComplete()) break;
        }

        return indicies;
    }

    private Device CreateLogicalDevice()
    {
        var indicies = GetQueueFamilyIndicies(PhysicalDevice);
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

        if (_renderer.WantValidationLayers)
            _renderer.Debug.AddValidationLayers(ref deviceCreateInfo);

        if (_renderer.vk.CreateDevice(PhysicalDevice, in deviceCreateInfo, null, out var device) != Result.Success)
            throw new Exception("Logical device creation failed!");

        if (_renderer.WantValidationLayers)
            SilkMarshal.Free((nint)deviceCreateInfo.PpEnabledLayerNames);
        
        return device;
    }

    public void Destroy()
    {
        _renderer.vk.DestroyDevice(LogicalDevice, null);
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
            return RenderUtil.VersionToString(_physicalDeviceProperties.ApiVersion);
        }
    }
}