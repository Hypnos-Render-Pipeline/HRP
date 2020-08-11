using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class TerrainPass : BaseRenderPass
    {
        [NodePin(PinType.InOut)]
        public TexturePin color = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.DefaultHDR, 0),
                                                                                       TexturePinDesc.SizeCastMode.ResizeToInput,
                                                                                       TexturePinDesc.ColorCastMode.FitToInput,
                                                                                       TexturePinDesc.SizeScale.Full));
        [NodePin(PinType.InOut)]
        public TexturePin depth = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                                                        TexturePinDesc.SizeCastMode.ResizeToInput,
                                                                        TexturePinDesc.ColorCastMode.Fixed,
                                                                        TexturePinDesc.SizeScale.Full));

        public override void Excute(RenderContext context)
        {
                context.CmdBuffer.SetRenderTarget(
                new[]{
                    (RenderTargetIdentifier)color.handle,
                    (RenderTargetIdentifier)depth.handle,
                }
                , depth.handle);

            context.Context.ExecuteCommandBuffer(context.CmdBuffer);
            context.CmdBuffer.Clear();

            var tms = GameObject.FindObjectsOfType<HRPTerrain>();
            foreach (var tm in tms)
            {
                if (tm.isActiveAndEnabled && tm.cb != null)
                    context.Context.ExecuteCommandBuffer(tm.cb);
            }
        }
    }

}