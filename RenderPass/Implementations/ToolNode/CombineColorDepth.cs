using UnityEngine;

namespace HypnosRenderPipeline.RenderPass
{
    public class CombineColorDepth : BaseToolNode
    {
        [NodePin(PinType.In, true)]
        public TexturePin color = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Default, 0),
                                                    SizeCastMode.ResizeToInput,
                                                    ColorCastMode.FitToInput,
                                                    SizeScale.Full);
        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                                    SizeCastMode.ResizeToInput,
                                                    ColorCastMode.FitToInput,
                                                    SizeScale.Full);

        [NodePin(PinType.Out)]
        public TexturePin combined = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.DefaultHDR, 24),
                                                    SizeCastMode.ResizeToInput,
                                                    ColorCastMode.FitToInput,
                                                    SizeScale.Full);

        public override void Excute(RenderContext context)
        {
            context.CmdBuffer.Blit(color, combined);
            context.CmdBuffer.Blit(depth, combined, MaterialWithName.depthBlit);
            context.Context.ExecuteCommandBuffer(context.CmdBuffer);
            context.CmdBuffer.Clear();
        }
    }
}