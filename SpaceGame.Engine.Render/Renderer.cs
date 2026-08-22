using Serilog;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

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
    public bool WantValidationLayers { get; set; }

    // INITIALIZATION FUNCTIONS
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
        Instance = RendererInit.CreateInstance(this);

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
        _swapchain = new VulkanSwapchain(this, window);
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
