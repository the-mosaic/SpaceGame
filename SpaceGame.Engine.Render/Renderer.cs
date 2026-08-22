using Serilog;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace SpaceGame.Engine.Render;

public unsafe class Renderer
{
    // vulkan related variables
    public VulkanContext Context { get; private set; }
    
    private readonly ILogger _log;

    // INITIALIZATION FUNCTIONS
    public Renderer(bool wantValidationLayers, IWindow window)
    {
        _log = SpaceGame.Logger!.ForContext<Renderer>();
        var vk = Vk.GetApi();

        Context = new VulkanContext
        {
            vk = vk,
            Instance = RendererInit.CreateInstance(ref wantValidationLayers, vk),
            WantValidationLayers = wantValidationLayers
        };

        // Create the debug manager.
        // This has to be done BEFORE CreateInstance() because it depends on
        // non-static methods of VulkanDebug
        Context.Debug = new VulkanDebug(Context);
        if (wantValidationLayers)
            Context.Debug.SetupDebugger();

        // Set up our surface extension...
        if (!vk.TryGetInstanceExtension(Context.Instance, out KhrSurface khrSurfaceExt))
            throw new Exception("KHR_surface extension not found, can't render anything");
        
        Context.ExtSurface = khrSurfaceExt;
        Context.Surface = window.VkSurface!.Create<AllocationCallbacks>(Context.Instance.ToHandle(), null).ToSurface();

        // Create our device. This little wrapper class will handle everything
        // for us just by using new().
        Context.Device = new VulkanDevice(Context);

        // Logging for debug stuff
        if (wantValidationLayers)
            _log.Information("Selected physical device {Name}, API Version {ApiVersion}", Context.Device.DeviceName, Context.Device.ApiVersionStr);
        
        // Now create the swapchain
        Context.Swapchain = new VulkanSwapchain(Context, window);
    }

    // RENDERING FUNCTIONS

    // CLEANUP FUNCTIONS
    public void Destroy() => Context.DestroyAll();
}
