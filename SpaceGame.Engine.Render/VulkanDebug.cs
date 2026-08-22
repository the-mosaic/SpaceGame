using Serilog;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using System.Runtime.InteropServices;

namespace SpaceGame.Engine.Render;

// aliases to make my life a bit easier
using MessageSeverity = DebugUtilsMessageSeverityFlagsEXT;
using MessageType = DebugUtilsMessageTypeFlagsEXT;
using MessengerCallbackData = DebugUtilsMessengerCallbackDataEXT;
using MessengerCallbackFn = DebugUtilsMessengerCallbackFunctionEXT;

public unsafe class VulkanDebug(Renderer renderer)
{
    public static readonly string[] ValidationLayers = ["VK_LAYER_KHRONOS_validation"];

    private ExtDebugUtils? _debugUtilsExt;
    private DebugUtilsMessengerEXT _debugMessenger;

    private readonly ILogger _log = SpaceGame.Logger!.ForContext<VulkanDebug>();
    private readonly Renderer _renderer = renderer;

    public bool ValidationLayersSupported()
    {
        // All this function does is go through every single layer that Vulkan
        // says is supported and checks to see if it is the validation ext
        // that we want.

        var layerCount = 0u;
        _renderer.vk.EnumerateInstanceLayerProperties(ref layerCount, null);

        var availableLayers = new LayerProperties[layerCount];
        fixed (LayerProperties* layersPointer = availableLayers)
            _renderer.vk.EnumerateInstanceLayerProperties(ref layerCount, layersPointer);

        var availableLayerNames = availableLayers.Select(layer => Marshal.PtrToStringAnsi((IntPtr)layer.LayerName));
        return ValidationLayers.All(availableLayerNames.Contains);
    }

    public void SetupDebugger()
    {
        if (!_renderer.vk.TryGetInstanceExtension(_renderer.Instance, out _debugUtilsExt)) return;

        var debugCreateInfo = GetDebugCreateInfo();

        if (_debugUtilsExt!.CreateDebugUtilsMessenger(_renderer.Instance, &debugCreateInfo, null, out var debugMessenger) != Result.Success)
            throw new Exception("Debug messenger creation failed!");

        _debugMessenger = debugMessenger;
    }

    // The create info here needs to be a pointer so that it's lifetime
    // doesn't run out before we run vkCreateInstance()
    public void AddValidationLayers(ref InstanceCreateInfo instanceInfo, DebugUtilsMessengerCreateInfoEXT* debugCreateInfo)
    {
        instanceInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
        instanceInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(ValidationLayers);

        *debugCreateInfo = GetDebugCreateInfo();
        instanceInfo.PNext = debugCreateInfo;
    }

    public void AddValidationLayers(ref DeviceCreateInfo deviceInfo)
    {
        deviceInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
        deviceInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(ValidationLayers);  
    }

    private DebugUtilsMessengerCreateInfoEXT GetDebugCreateInfo()
    {
        return new DebugUtilsMessengerCreateInfoEXT()
        {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity = MessageSeverity.VerboseBitExt | MessageSeverity.WarningBitExt | MessageSeverity.ErrorBitExt,
            MessageType = MessageType.GeneralBitExt | MessageType.PerformanceBitExt | MessageType.ValidationBitExt,
            PfnUserCallback = (MessengerCallbackFn)DebugCallback
        };
    }

    private uint DebugCallback(MessageSeverity messageSeverity, MessageType messageTypes, MessengerCallbackData* pCallbackData, void* pUserData)
    {
        _log.Information("{Message}", Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage));
        return Vk.False;
    }

    public void Destroy()
    {
        _debugUtilsExt?.DestroyDebugUtilsMessenger(_renderer.Instance, _debugMessenger, null);
    }
}
