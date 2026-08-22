using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace SpaceGame.Engine.Render;

public unsafe class VulkanContext
{
    public required Vk Vk { get; set; }
    public required Instance Instance { get; set; }
    public VulkanDebug? Debug { get; set; }
    public VulkanDevice? Device { get; set; }
    public VulkanSwapchain? Swapchain { get; set; }
    public SurfaceKHR? Surface { get; set; }
    public KhrSurface? ExtSurface { get; set; }

    public bool WantValidationLayers { get; set; }

    public void DestroyAll()
    {
        Swapchain?.Destroy();
        Device?.Destroy();

        if (Surface is not null)
            ExtSurface?.DestroySurface(Instance, Surface.Value, null);
        
        Debug?.Destroy();
        Vk.DestroyInstance(Instance, null);
        Vk.Dispose();
    }
}