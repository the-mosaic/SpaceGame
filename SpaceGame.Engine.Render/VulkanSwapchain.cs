using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace SpaceGame.Engine.Render;

public unsafe class VulkanSwapchain
{
    private readonly Renderer _renderer;
    private readonly IWindow _window;
    private readonly KhrSwapchain _khrSwapchainExt;
    private SwapchainKHR _self;
    private Image[]? _images;
    private Format? _imageFormat;
    private Extent2D _extent;
    private ImageView[] _imageViews;

    public VulkanSwapchain(Renderer renderer, IWindow window)
    {
        _window = window;
        _renderer = renderer;
        
        if (!renderer.vk.TryGetDeviceExtension(renderer.Instance, renderer.Device.LogicalDevice, out _khrSwapchainExt))
            throw new Exception("KHR_Swapchain extension not found!");
        
        _self = CreateSwapchain();
        _imageViews = CreateImageViews();
    }

    private static SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] availableFormats)
    {
        foreach (var surfaceFormat in availableFormats)
        {
            if (surfaceFormat.Format == Format.B8G8R8A8Srgb && surfaceFormat.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
                return surfaceFormat;
        }

        return availableFormats[0];
    }

    private static PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] availablePresentModes)
    {
        foreach (var presentMode in availablePresentModes)
        {
            if (presentMode == PresentModeKHR.MailboxKhr)
                return presentMode;
        }

        return PresentModeKHR.FifoKhr;
    }

    private static Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities, Vector2D<int> windowSize)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
            return capabilities.CurrentExtent;
        
        var width = windowSize.X;
        var height = windowSize.Y;

        var actualExtent = new Extent2D((uint)width, (uint)height);

        actualExtent.Width = Math.Clamp(actualExtent.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
        actualExtent.Height = Math.Clamp(actualExtent.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);

        return actualExtent;
    }

    private SwapchainKHR CreateSwapchain()
    {
        var supportDetails = _renderer.Device.GetSwapchainSupport();

        var surfaceFormat = ChooseSwapSurfaceFormat(supportDetails.Formats);
        var presentMode = ChooseSwapPresentMode(supportDetails.PresentModes);
        var extent = ChooseSwapExtent(supportDetails.Capabilities, _window.GetFullSize());

        var imageCount = supportDetails.Capabilities.MinImageCount + 1;

        if (supportDetails.Capabilities.MaxImageCount > 0 && imageCount > supportDetails.Capabilities.MaxImageCount)
            imageCount = supportDetails.Capabilities.MaxImageCount;
        
        var swapchainCreateInfo = new SwapchainCreateInfoKHR()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _renderer.Surface,

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

        var familyIndicies = _renderer.Device.GetQueueFamilyIndicies();
        
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

        if (_khrSwapchainExt.CreateSwapchain(_renderer.Device.LogicalDevice, in swapchainCreateInfo, null, out var swapchain) != Result.Success)
        {
            throw new Exception("Swapchain creation failed");
        }
        
        _khrSwapchainExt.GetSwapchainImages(_renderer.Device.LogicalDevice, swapchain, ref imageCount, null);
        _images = new Image[imageCount];

        fixed (Image* ptr = _images)
            _khrSwapchainExt.GetSwapchainImages(_renderer.Device.LogicalDevice, swapchain, ref imageCount, ptr);

        _imageFormat = surfaceFormat.Format;
        _extent = extent;

        return swapchain;
    }

    public ImageView[] CreateImageViews()
    {
        var result = new ImageView[_images!.Length];

        for (var i = 0; i < result.Length; i++)
        {
            var imageViewInfo = new ImageViewCreateInfo()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _images[i],
                ViewType = ImageViewType.Type2D,
                Format = _imageFormat!.Value,
                Components =
                {
                    R = ComponentSwizzle.Identity,
                    G = ComponentSwizzle.Identity,
                    B = ComponentSwizzle.Identity,
                    A = ComponentSwizzle.Identity
                },
                SubresourceRange =
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            if (_renderer.vk.CreateImageView(_renderer.Device.LogicalDevice, in imageViewInfo, null, out result[i]) != Result.Success)
                throw new Exception("Image view creation failed!");
        }

        return result;
    }

    public void Destroy()
    {
        foreach (var imageView in _imageViews)
            _renderer.vk.DestroyImageView(_renderer.Device.LogicalDevice, imageView, null);

        _khrSwapchainExt.DestroySwapchain(_renderer.Device.LogicalDevice, _self, null);
    }
}