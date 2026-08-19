using Serilog;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using System.Runtime.InteropServices;

namespace SpaceGame.Engine;

// aliases to make my life a bit easier
using MessageSeverity = DebugUtilsMessageSeverityFlagsEXT;
using MessageType = DebugUtilsMessageTypeFlagsEXT;
using MessengerCallbackData = DebugUtilsMessengerCallbackDataEXT;
using MessengerCallbackFn = DebugUtilsMessengerCallbackFunctionEXT;

public unsafe class RenderDebug(Renderer renderer)
{
    public static readonly string[] ValidationLayers = ["VK_LAYER_KHRONOS_validation"];

    private ExtDebugUtils? _debugUtils;
    private DebugUtilsMessengerEXT _debugMessenger;

    private readonly ILogger _log = SpaceGame.Logger!.ForContext<RenderDebug>();
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
        if (!_renderer.vk.TryGetInstanceExtension(_renderer.instance, out _debugUtils)) return;

        var debugCreateInfo = GetDebugCreateInfo();

        if (_debugUtils!.CreateDebugUtilsMessenger(_renderer.instance, &debugCreateInfo, null, out var debugMessenger) != Result.Success)
            throw new Exception("Debug messenger creation failed!");

        _debugMessenger = debugMessenger;
    }

    // Takes some instance create info without validation layers enabled and
    // sets up the neccessary properties to have validation layers.
    public void AddValidationLayers(ref InstanceCreateInfo info, DebugUtilsMessengerCreateInfoEXT* debugCreateInfo)
    {
        info.EnabledLayerCount = (uint)ValidationLayers.Length;
        info.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(ValidationLayers);

        *debugCreateInfo = GetDebugCreateInfo();
        info.PNext = debugCreateInfo;
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
        _debugUtils?.DestroyDebugUtilsMessenger(_renderer.instance, _debugMessenger, null);
    }
}
