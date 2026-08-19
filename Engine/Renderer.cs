using Serilog;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using System.Runtime.InteropServices;

namespace SpaceGame.Engine;

public unsafe class Renderer
{
    public readonly Vk vk;
    public Instance instance;
    private readonly ILogger _log;
    private bool _wantValidationLayers;
    private RenderDebug _debugMgr;

    public Renderer(bool wantValidationLayers)
    {
        _wantValidationLayers = wantValidationLayers;

        _log = SpaceGame.Logger!.ForContext<Renderer>();
        vk = Vk.GetApi();

        // initialization
        _debugMgr = new RenderDebug(this);
        instance = CreateInstance();

        if (_wantValidationLayers)
            _debugMgr.SetupDebugger();
    }

    // INITIALIZATION FUNCTIONS
    private Instance CreateInstance()
    {
        if (_wantValidationLayers && !_debugMgr.ValidationLayersSupported())
        {
            // Should we exit here or just do a warning?
            _log.Warning("Validation layers requested, but the system does not support the required layers. Continuing without validation.");
            _wantValidationLayers = false;
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

            // Not sure if we actually need Vulkan 1.2 here. I'm just too
            // scared to use anything earlier because learnvulkan uses 1.2 LOL
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
        if (_wantValidationLayers)
        {
            // this function populates all the fields we need i hope
            _debugMgr.AddValidationLayers(ref instanceInfo, &debugCreateInfo);
        }

        if (vk.CreateInstance(in instanceInfo, null, out var result) != Result.Success)
            throw new Exception("Vulkan creation failed");

        // grumble grumble
        Marshal.FreeHGlobal((nint)appInfo.PApplicationName);
        Marshal.FreeHGlobal((nint)appInfo.PEngineName);
        SilkMarshal.Free((nint)instanceInfo.PpEnabledExtensionNames);

        if (_wantValidationLayers)
            SilkMarshal.Free((nint)instanceInfo.PpEnabledLayerNames);

        // This doesn't directly set the value of _instance to avoid VS yelling
        // at me for having a potential null variable. I need to set it
        // directly in the constructor for it to shut up
        return result;
    }

    private string[] GetRequiredExtensions()
    {
        var window = SpaceGame.Instance!.SpWindow!;
        var glfwExntensions = window.VkSurface!.GetRequiredExtensions(out var glfwExtensionCount);
        var extensions = SilkMarshal.PtrToStringArray((IntPtr)glfwExntensions, (int)glfwExtensionCount);

        if (_wantValidationLayers)
            extensions = [..extensions, ExtDebugUtils.ExtensionName];

        return extensions;
    }

    // RENDERING FUNCTIONS

    // CLEANUP FUNCTIONS
    public void Destroy()
    {
        _debugMgr.Destroy();
        vk.DestroyInstance(instance, null);
        vk.Dispose();
    }
}
