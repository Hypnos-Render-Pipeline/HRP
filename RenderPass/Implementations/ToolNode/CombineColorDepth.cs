using UnityEngine;

namespace HypnosRenderPipeline.RenderPass
{
    public class CombineColorDepth : BaseToolNode
    {
        [NodePin(PinType.In, true)]
        public TexturePin color = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Default, 0),
                                                                                       TexturePinDesc.SizeCastMode.ResizeToInput,
                                                                                       TexturePinDesc.ColorCastMode.FitToInput,
                                                                                       TexturePinDesc.SizeScale.Full));
        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                                                        TexturePinDesc.SizeCastMode.ResizeToInput,
                                                                        TexturePinDesc.ColorCastMode.FitToInput,
                                                                        TexturePinDesc.SizeScale.Full));

        [NodePin(PinType.Out)]
        public TexturePin combined = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.DefaultHDR, 24),
                                                                                       TexturePinDesc.SizeCastMode.ResizeToInput,
                                                                                       TexturePinDesc.ColorCastMode.FitToInput,
                                                                                       TexturePinDesc.SizeScale.Full));

        public override void Excute(RenderContext context)
        {
            context.CmdBuffer.Blit(color.handle, combined.handle);
            context.CmdBuffer.Blit(depth.handle, combined.handle, MaterialWithName.depthBlit);
            context.Context.ExecuteCommandBuffer(context.CmdBuffer);
            context.CmdBuffer.Clear();
        }
    }
}