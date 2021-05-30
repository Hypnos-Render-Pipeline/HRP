using UnityEngine;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline.RenderPass
{
    public class CombineColorDepth : BaseToolNode
    {
        [NodePin(PinType.In, true)]
        public TexturePin color = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.DefaultHDR, 0),
                                                    SizeCastMode.ResizeToInput,
                                                    ColorCastMode.FitToInput,
                                                    SizeScale.Full);
        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                                    SizeCastMode.ResizeToInput,
                                                    ColorCastMode.Fixed,
                                                    SizeScale.Full);

        [NodePin(PinType.Out)]
        public TexturePin combined = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.DefaultHDR, 24),
                                                    SizeCastMode.ResizeToInput,
                                                    ColorCastMode.FitToInput,
                                                    SizeScale.Full);

        public bool TemporalAccumulate = false;

        static MaterialWithName toneMat = new MaterialWithName("Hidden/ACES");

        public override void Execute(RenderContext context)
        {
            if (TemporalAccumulate)
            {
                int _History_Final_Result = Shader.PropertyToID("_History_Final_Result");
                var tex = context.resourcesPool.GetTexture(_History_Final_Result, color.desc.basicDesc);

                context.commandBuffer.SetGlobalTexture("_History_Final_Result", tex);
                context.commandBuffer.Blit(color, combined, toneMat, 1);
                context.commandBuffer.Blit(combined, tex);
                context.commandBuffer.Blit(tex, combined);
            }
            else
            {
                context.commandBuffer.Blit(color, combined);
            }

            context.commandBuffer.Blit(depth, combined, MaterialWithName.depthBlit);
        }
    }
}