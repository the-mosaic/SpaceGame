using Silk.NET.Shaderc;
using Silk.NET.Vulkan;

namespace SpaceGame.Engine.Render;

public unsafe class VulkanPipeline
{
    private Pipeline _self;

    public VulkanPipeline(VulkanContext context)
    {
        var vertexShader = new VulkanShader(context, "resources/shaders/shader.vert", ShaderKind.VertexShader);
        var fragmentShader = new VulkanShader(context, "resources/shaders/shader.frag", ShaderKind.FragmentShader);

        PipelineShaderStageCreateInfo vertexStageInfo;
        PipelineShaderStageCreateInfo fragmentStageInfo;
        fixed (char* pName = "main")
        {
            vertexStageInfo = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = vertexShader.Module,
                PName = (byte*)pName
            };

            fragmentStageInfo = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = fragmentShader.Module,
                PName = (byte*)pName
            };
        }
        
        PipelineShaderStageCreateInfo[] shaderStages = [vertexStageInfo, fragmentStageInfo];

        fragmentShader.Destroy();
        vertexShader.Destroy();
    }
}