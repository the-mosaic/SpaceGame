using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;

namespace SpaceGame.Engine.Render;

public unsafe class RendererInit
{
    public static Instance CreateInstance(Renderer renderer)
    {
        var log = SpaceGame.Logger!.ForContext<RendererInit>();

        if (renderer.WantValidationLayers && !renderer.Debug.ValidationLayersSupported())
        {
            // Should we exit here or just do a warning?
            log.Warning("Validation layers requested, but the system does not support the required layers. Continuing without validation.");
            renderer.WantValidationLayers = false;
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

        var extensions = GetRequiredExtensions(renderer);
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
        if (renderer.WantValidationLayers)
            renderer.Debug.AddValidationLayers(ref instanceInfo, &debugCreateInfo);

        if (renderer.vk.CreateInstance(in instanceInfo, null, out var result) != Result.Success)
            throw new Exception("Vulkan creation failed");

        // grumble grumble
        Marshal.FreeHGlobal((nint)appInfo.PApplicationName);
        Marshal.FreeHGlobal((nint)appInfo.PEngineName);
        SilkMarshal.Free((nint)instanceInfo.PpEnabledExtensionNames);

        if (renderer.WantValidationLayers)
            SilkMarshal.Free((nint)instanceInfo.PpEnabledLayerNames);
        
        return result;
    }

    private static string[] GetRequiredExtensions(Renderer renderer)
    {
        var glfwExntensions = SpaceGame.Instance!.SpWindow.VkSurface!.GetRequiredExtensions(out var glfwExtensionCount);
        var extensions = SilkMarshal.PtrToStringArray((IntPtr)glfwExntensions, (int)glfwExtensionCount);

        if (renderer.WantValidationLayers)
            extensions = [..extensions, ExtDebugUtils.ExtensionName];

        return extensions;
    }
}