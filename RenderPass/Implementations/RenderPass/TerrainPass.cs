using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class TerrainPass : BaseRenderPass
    {
        [NodePin(PinType.InOut)]
        public TexturePin color = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.DefaultHDR, 0),
                                                        SizeCastMode.ResizeToInput,
                                                        ColorCastMode.FitToInput,
                                                        SizeScale.Full);
        [NodePin(PinType.InOut)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                                        SizeCastMode.ResizeToInput,
                                                        ColorCastMode.Fixed,
                                                        SizeScale.Full);

        public bool gameCameraCull = true;

        HRPTerrain terrain = null;

        public override void Excute(RenderContext context)
        {
            context.CmdBuffer.SetRenderTarget(new RenderTargetIdentifier[]{ color, depth }, depth);

            var cam = gameCameraCull ? Camera.main ?? context.RenderCamera : context.RenderCamera;

            FrustumCulling.SetCullingCamera(context.CmdBuffer, cam);

            context.Context.ExecuteCommandBuffer(context.CmdBuffer);
            context.CmdBuffer.Clear();

            if (terrain != null)
            {
                if (terrain.isActiveAndEnabled && terrain.cb != null)
                {
                    terrain.MoveTerrain(context.CmdBuffer, cam);
                    context.Context.ExecuteCommandBuffer(context.CmdBuffer);
                    context.CmdBuffer.Clear();
                    context.Context.ExecuteCommandBuffer(terrain.cb);
                }
            }
            else
            {
                terrain = GameObject.FindObjectOfType<HRPTerrain>();
            }
        }
    }

}