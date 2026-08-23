using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;

namespace SpaceGame.Engine.Render;

public unsafe class RendererInit
{
    public static Instance CreateInstance(ref bool wantValidationLayers, Vk vk)
    {
        var log = SpaceGame.Logger!.ForContext<RendererInit>();

        if (wantValidationLayers && !VulkanDebug.ValidationLayersSupported(vk))
        {
            // Should we exit here or just do a warning?
            log.Warning("Validation layers requested, but the system does not support the required layers. Continuing without validation.");
            wantValidationLayers = false;
        }

        // I really don't like having to use Marshal stuff here, but apparently
        // Silk.NET only supports using Spans for their OpenGL APIs(!!), so I'm
        // stuck with this ugly code for now.
        ApplicationInfo appInfo;

        fixed(char* engineName = "No Engine")
        fixed(char* applicationName = "Space game")
        {
            appInfo = new ApplicationInfo()
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)applicationName,
                ApplicationVersion = SpaceGame.ApplicationVersion,
                PEngineName = (byte*)engineName,
                EngineVersion = SpaceGame.ApplicationVersion,
                ApiVersion = Vk.Version12
            };
        }

        var extensions = GetRequiredExtensions(wantValidationLayers);
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
        if (wantValidationLayers)
            VulkanDebug.AddValidationLayers(ref instanceInfo, &debugCreateInfo);

        if (vk.CreateInstance(in instanceInfo, null, out var result) != Result.Success)
            throw new Exception("Vulkan creation failed");

        // grumble grumble
        SilkMarshal.Free((nint)instanceInfo.PpEnabledExtensionNames);

        if (wantValidationLayers)
            SilkMarshal.Free((nint)instanceInfo.PpEnabledLayerNames);
        
        return result;
    }

    private static string[] GetRequiredExtensions(bool wantValidationLayers)
    {
        var glfwExntensions = SpaceGame.Instance!.SpWindow.VkSurface!.GetRequiredExtensions(out var glfwExtensionCount);
        var extensions = SilkMarshal.PtrToStringArray((IntPtr)glfwExntensions, (int)glfwExtensionCount);

        if (wantValidationLayers)
            extensions = [..extensions, ExtDebugUtils.ExtensionName];

        return extensions;
    }
}