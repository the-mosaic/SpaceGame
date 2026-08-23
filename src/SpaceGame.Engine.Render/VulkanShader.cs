using System.Runtime.InteropServices;
using Silk.NET.Shaderc;
using Silk.NET.Vulkan;

namespace SpaceGame.Engine.Render;

public unsafe class VulkanShader
{
    private VulkanContext _context;
    private ShaderModule _self;
    public ShaderModule Module => _self;

    public VulkanShader(VulkanContext context, string fileName, ShaderKind kind)
    {
        if (context.Device is null)
            throw new Exception("Attempted to create shader before device creation!");

        _context = context;

        byte[] code = CompileToSpirv(context, fileName, kind);
        ShaderModuleCreateInfo shaderInfo;

        fixed(byte* codePtr = code)
        {
            shaderInfo = new ShaderModuleCreateInfo
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)code.Length,
                PCode = (uint*)codePtr
            };
        }
        
        _self = context.Device.CreateShaderModule(shaderInfo);
    }

    public void Destroy()
    {
        _context.Device!.DestroyShaderModule(_self);
    }

    private byte[] CompileToSpirv(VulkanContext context, string fileName, ShaderKind kind)
    {
        var shaderText = File.ReadAllBytes(fileName);
        var compiler = context.Shaderc.CompilerInitialize();
        var compileOptions = context.Shaderc.CompileOptionsInitialize();

        CompilationResult* result = null;

        try
        {
            fixed(byte* textPtr = shaderText)
                result = context.Shaderc.CompileIntoSpv(compiler, textPtr, (nuint)shaderText.Length, kind, fileName, "main", compileOptions);

            if (context.Shaderc.ResultGetCompilationStatus(result) != CompilationStatus.Success)
            {
                var errorMessage = Marshal.PtrToStringUTF8((nint)context.Shaderc.ResultGetErrorMessage(result));
                throw new Exception($"Shader compilation failed: {errorMessage}");
            }

            var spirvLength = context.Shaderc.ResultGetLength(result);
            var bytes = context.Shaderc.ResultGetBytes(result);
            var spirv = new byte[spirvLength];

            Marshal.Copy((nint)bytes, spirv, 0, spirv.Length);
            return spirv;
        }
        finally
        {
            if (result != null)
                context.Shaderc.ResultRelease(result);
            
            context.Shaderc.CompileOptionsRelease(compileOptions);
            context.Shaderc.CompilerRelease(compiler);
        }
    }
}