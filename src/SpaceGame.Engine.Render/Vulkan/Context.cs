using Silk.NET.Shaderc;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace SpaceGame.Engine.Render.Vulkan;

public unsafe class VulkanContext
{
    public required Vk Vk { get; set; }
    public required Shaderc Shaderc { get; set; }
    public required Instance Instance { get; set; }
    public VulkanDebug? Debug { get; set; }
    public VulkanDevice? Device { get; set; }
    public VulkanSwapchain? Swapchain { get; set; }
    public SurfaceKHR? Surface { get; set; }
    public KhrSurface? ExtSurface { get; set; }
    public VulkanPipeline? Pipeline { get; set; }

    public bool WantValidationLayers { get; set; }

    public void DestroyAll()
    {
        Pipeline?.Destroy();
        Swapchain?.Destroy();
        Device?.Destroy();

        if (Surface is SurfaceKHR surface)
            ExtSurface?.DestroySurface(Instance, surface, null);
        
        Debug?.Destroy();
        Vk.DestroyInstance(Instance, null);
        Vk.Dispose();
    }
}