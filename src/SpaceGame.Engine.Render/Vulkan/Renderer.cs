using Serilog;
using Silk.NET.Shaderc;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace SpaceGame.Engine.Render.Vulkan;

public unsafe class VulkanRenderer: IRenderer
{
    // vulkan related variables
    public VulkanContext Context { get; private set; }
    
    private readonly ILogger _log;

    // INITIALIZATION FUNCTIONS
    public VulkanRenderer(bool wantValidationLayers, IWindow window)
    {
        _log = SpaceGame.Logger!.ForContext<VulkanRenderer>();
        var vk = Vk.GetApi();

        Context = new VulkanContext
        {
            Vk = vk,
            Shaderc = Shaderc.GetApi(),
            Instance = VulkanInit.CreateInstance(ref wantValidationLayers, vk),
            WantValidationLayers = wantValidationLayers
        };
        
        Context.Debug = new VulkanDebug(Context);

        if (!vk.TryGetInstanceExtension(Context.Instance, out KhrSurface khrSurfaceExt))
            throw new Exception("KHR_surface extension not found, can't render anything");
        
        Context.ExtSurface = khrSurfaceExt;
        Context.Surface = window.VkSurface!.Create<AllocationCallbacks>(Context.Instance.ToHandle(), null).ToSurface();
        Context.Device = new VulkanDevice(Context);

        if (wantValidationLayers)
            _log.Information("Selected physical device {Name}, API Version {ApiVersion}", Context.Device.DeviceName, Context.Device.ApiVersionStr);
        
        Context.Swapchain = new VulkanSwapchain(Context, window);
        Context.Pipeline = new VulkanPipeline(Context);
    }

    // RENDERING FUNCTIONS
    public void Render() {}

    // CLEANUP FUNCTIONS
    public void Destroy() => Context.DestroyAll();
}
