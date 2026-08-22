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
    private VulkanDevice _device;
    private KhrSurface _khrSurfaceExt;
    public KhrSurface KhrSurfaceExt { get => _khrSurfaceExt; }
    public SurfaceKHR Surface { get; private set; }
    private KhrSwapchain _khrSwapchainExt;
    private SwapchainKHR _swapchain;
    private Image[]? _swapchainImages;
    private Format? _swapchainImageFormat;
    private Extent2D _swapchainExtent;
    
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
        _device = new(this);

        // Logging for debug stuff
        if (WantValidationLayers)
            _log.Information("Selected physical device {Name}, API Version {ApiVersion}", _device.DeviceName, _device.ApiVersionStr);
        
        // Now create the swapchain
        if (!vk.TryGetDeviceExtension(Instance, _device.LogicalDevice, out _khrSwapchainExt))
            throw new Exception("KHR_Swapchain extension not found!");
        
        _swapchain = CreateSwapchain();
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

    private SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] availableFormats)
    {
        foreach (var surfaceFormat in availableFormats)
        {
            if (surfaceFormat.Format == Format.B8G8R8A8Srgb && surfaceFormat.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
                return surfaceFormat;
        }

        return availableFormats[0];
    }

    private PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] availablePresentModes)
    {
        foreach (var presentMode in availablePresentModes)
        {
            if (presentMode == PresentModeKHR.MailboxKhr)
                return presentMode;
        }

        return PresentModeKHR.FifoKhr;
    }

    private Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
            return capabilities.CurrentExtent;
        
        var width = _window.GetFullSize().X;
        var height = _window.GetFullSize().Y;

        var actualExtent = new Extent2D((uint)width, (uint)height);

        actualExtent.Width = Math.Clamp(actualExtent.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
        actualExtent.Height = Math.Clamp(actualExtent.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);

        return actualExtent;
    }

    private SwapchainKHR CreateSwapchain()
    {
        var supportDetails = _device.GetSwapchainSupport();

        var surfaceFormat = ChooseSwapSurfaceFormat(supportDetails.Formats);
        var presentMode = ChooseSwapPresentMode(supportDetails.PresentModes);
        var extent = ChooseSwapExtent(supportDetails.Capabilities);

        var imageCount = supportDetails.Capabilities.MinImageCount + 1;

        if (supportDetails.Capabilities.MaxImageCount > 0 && imageCount > supportDetails.Capabilities.MaxImageCount)
            imageCount = supportDetails.Capabilities.MaxImageCount;
        
        var swapchainCreateInfo = new SwapchainCreateInfoKHR()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = Surface,

            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,

            ImageSharingMode = SharingMode.Exclusive,
            QueueFamilyIndexCount = 0,

            PreTransform = supportDetails.Capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = presentMode,
            Clipped = true,
            OldSwapchain = default
        };

        var familyIndicies = _device.GetQueueFamilyIndicies();
        
        if (familyIndicies.GraphicsFamily != familyIndicies.PresentFamily)
        {
            var familyIndiciesPtr = stackalloc[] {
                familyIndicies.GraphicsFamily!.Value,
                familyIndicies.PresentFamily!.Value
            };

            swapchainCreateInfo.ImageSharingMode = SharingMode.Concurrent;
            swapchainCreateInfo.QueueFamilyIndexCount = 2;
            swapchainCreateInfo.PQueueFamilyIndices = familyIndiciesPtr;
        }

        if (_khrSwapchainExt.CreateSwapchain(_device.LogicalDevice, in swapchainCreateInfo, null, out var swapchain) != Result.Success)
        {
            throw new Exception("Swapchain creation failed");
        }
        
        _khrSwapchainExt.GetSwapchainImages(_device.LogicalDevice, swapchain, ref imageCount, null);
        _swapchainImages = new Image[imageCount];

        fixed (Image* ptr = _swapchainImages)
            _khrSwapchainExt.GetSwapchainImages(_device.LogicalDevice, swapchain, ref imageCount, ptr);

        _swapchainImageFormat = surfaceFormat.Format;
        _swapchainExtent = extent;

        return swapchain;
    }

    // RENDERING FUNCTIONS

    // CLEANUP FUNCTIONS
    public void Destroy()
    {
        _khrSwapchainExt.DestroySwapchain(_device.LogicalDevice, _swapchain, null);
        _device.Destroy();
        Debug.Destroy();

        KhrSurfaceExt.DestroySurface(Instance, Surface, null);
        vk.DestroyInstance(Instance, null);
        vk.Dispose();
    }
}
