using Silk.NET.Shaderc;
using Silk.NET.Vulkan;

namespace SpaceGame.Engine.Render.Vulkan;

public unsafe class VulkanPipeline
{
    private readonly VulkanContext _context;
    private Pipeline _self;
    private PipelineLayout _layout;

    public VulkanPipeline(VulkanContext context)
    {
        if (context.Swapchain is null)
            throw new Exception("Attempted to create pipeline before swapchain creation!");

        if (context.Device is null)
            throw new Exception("Attempted to create pipeline before device creation!");
        
        _context = context;

        var vertexShader = new VulkanShader(context, "resources/shaders/shader.vert", ShaderKind.VertexShader);
        var fragmentShader = new VulkanShader(context, "resources/shaders/shader.frag", ShaderKind.FragmentShader);

        var vertexStageInfo = vertexShader.GetPipelineStageCreateInfo();
        var fragmentStageInfo = fragmentShader.GetPipelineStageCreateInfo();
        
        PipelineShaderStageCreateInfo[] shaderStages = [vertexStageInfo, fragmentStageInfo];

        // Dynamic state
        DynamicState[] dynamicStates = [DynamicState.Viewport, DynamicState.Scissor];
        PipelineDynamicStateCreateInfo dynamicStateInfo;

        fixed (DynamicState* dynamicStatesPtr = dynamicStates)
        {
            dynamicStateInfo = new PipelineDynamicStateCreateInfo
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = (uint)dynamicStates.Length,
                PDynamicStates = dynamicStatesPtr
            };
        }
        

        // Description of our vertex data format.
        var vertexInputInfo = new PipelineVertexInputStateCreateInfo
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = 0,
            VertexAttributeDescriptionCount = 0
        };

        // Input assembly
        var inputAssemblyInfo = new PipelineInputAssemblyStateCreateInfo
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = PrimitiveTopology.TriangleList,
            PrimitiveRestartEnable = false
        };

        // Viewport settings
        var viewport = new Viewport
        {
            X = 0f,
            Y = 0f,
            Width = context.Swapchain.Extent.Width,
            Height = context.Swapchain.Extent.Height,
            MinDepth = 0f,
            MaxDepth = 0f
        };

        // Scissor mask setup
        var scissorMask = new Rect2D
        {
            Offset = new(0, 0),
            Extent = context.Swapchain.Extent
        };

        // viewport state
        var viewportState = new PipelineViewportStateCreateInfo
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            PViewports = &viewport,
            ScissorCount = 1,
            PScissors = &scissorMask
        };

        // Rasterizer
        var rasterizerInfo = new PipelineRasterizationStateCreateInfo
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            DepthClampEnable = true,
            RasterizerDiscardEnable = false,
            PolygonMode = PolygonMode.Fill,
            LineWidth = 1f,
            CullMode = CullModeFlags.BackBit,
            FrontFace = FrontFace.Clockwise,
            DepthBiasEnable = false
        };

        var multisampleInfo = new PipelineMultisampleStateCreateInfo
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            SampleShadingEnable = false,
            RasterizationSamples = SampleCountFlags.Count1Bit
        };

        var colorBlendAttachment = new PipelineColorBlendAttachmentState
        {
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
            BlendEnable = false
        };

        var colorBlendInfo = new PipelineColorBlendStateCreateInfo
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            LogicOpEnable = false,
            AttachmentCount = 1,
            PAttachments = &colorBlendAttachment
        };

        var pipelineLayoutInfo = new PipelineLayoutCreateInfo
        {
            SType = StructureType.PipelineLayoutCreateInfo
        };

        if (context.Vk.CreatePipelineLayout(context.Device.LogicalDevice, &pipelineLayoutInfo, null, out _layout) != Result.Success)
            throw new Exception("Pipeline layout creation failed!");

        fragmentShader.Destroy();
        vertexShader.Destroy();
    }

    public void Destroy()
    {
        _context.Vk.DestroyPipelineLayout(_context.Device!.LogicalDevice, _layout, null);
    }
}