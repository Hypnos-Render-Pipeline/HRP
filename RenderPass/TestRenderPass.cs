using UnityEngine;

namespace HypnosRenderPipeline.RenderPass
{
    public class LightingPass : BaseRenderPass
    {
        [NodePin(PinType.In)]
        public LightListPin lights = new LightListPin();

        [NodePin(PinType.In)]
        public TexturePin depth = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                                                        TexturePinDesc.SizeCastMode.ResizeToInput,
                                                                        TexturePinDesc.ColorCastMode.Fixed,
                                                                        TexturePinDesc.SizeScale.Full));

        [NodePin(PinType.In)]
        public TexturePin baseColor_roughness = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                                       TexturePinDesc.SizeCastMode.ResizeToInput,
                                                                                       TexturePinDesc.ColorCastMode.FitToInput,
                                                                                       TexturePinDesc.SizeScale.Full));
        [NodePin(PinType.In)]
        public TexturePin normal_metallic = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                                       TexturePinDesc.SizeCastMode.ResizeToInput,
                                                                                       TexturePinDesc.ColorCastMode.FitToInput,
                                                                                       TexturePinDesc.SizeScale.Full));
        [NodePin(PinType.In)]
        public TexturePin emission = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                                       TexturePinDesc.SizeCastMode.ResizeToInput,
                                                                                       TexturePinDesc.ColorCastMode.FitToInput,
                                                                                       TexturePinDesc.SizeScale.Full));

        [NodePin(PinType.Out)]
        public TexturePin lightingResult = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.DefaultHDR, 0),
                                                                                       TexturePinDesc.SizeCastMode.Fixed,
                                                                                       TexturePinDesc.ColorCastMode.Fixed,
                                                                                       TexturePinDesc.SizeScale.Full));


        static MaterialWithName lightingMat = new MaterialWithName("Hidden/DeferredLighting");

        public override void Excute(RenderContext context)
        {
            var cam = context.RenderCamera;

            context.CmdBuffer.SetGlobalMatrix("_V", cam.worldToCameraMatrix);
            context.CmdBuffer.SetGlobalMatrix("_V_Inv", cam.cameraToWorldMatrix);
            context.CmdBuffer.SetGlobalMatrix("_VP_Inv", (GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * cam.worldToCameraMatrix).inverse);
            context.CmdBuffer.SetGlobalTexture("_DepthTex", depth.handle);
            context.CmdBuffer.SetGlobalTexture("_BaseColorTex", baseColor_roughness.handle);
            context.CmdBuffer.SetGlobalTexture("_NormalTex", normal_metallic.handle);
            context.CmdBuffer.SetGlobalTexture("_EmissionTex", emission.handle);
            context.CmdBuffer.Blit(null, lightingResult.handle, lightingMat, 0);
            context.Context.ExecuteCommandBuffer(context.CmdBuffer);
            context.CmdBuffer.Clear();
        }
    }
}