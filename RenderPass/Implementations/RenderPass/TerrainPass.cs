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

        public TerrainMesh terrain;

        public override void Excute(RenderContext context)
        {
            if (terrain == null || terrain.cb == null) return;
                context.CmdBuffer.SetRenderTarget(
                new[]{
                    (RenderTargetIdentifier)color.handle,
                    (RenderTargetIdentifier)depth.handle,
                }
                , depth.handle);

            context.Context.ExecuteCommandBuffer(context.CmdBuffer);
            context.CmdBuffer.Clear();

            context.Context.ExecuteCommandBuffer(terrain.cb);
        }
    }

}