using UnityEngine;

namespace HypnosRenderPipeline.RenderPass
{
    public class LightingPass : BaseRenderPass
    {

        [NodePin(PinType.In)]
        public BufferPin<LightStructGPU> lightBuffer = new BufferPin<LightStructGPU>(200);

        [NodePin(PinType.In)]
        public LightListPin lights = new LightListPin();

        [NodePin(PinType.In)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                                                SizeCastMode.ResizeToInput,
                                                                ColorCastMode.Fixed,
                                                                SizeScale.Full);

        [NodePin(PinType.In)]
        public TexturePin baseColor_roughness = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                SizeCastMode.ResizeToInput,
                                                                ColorCastMode.FitToInput,
                                                                SizeScale.Full);
    [NodePin(PinType.In)]
        public TexturePin normal_metallic = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                SizeCastMode.ResizeToInput,
                                                                ColorCastMode.FitToInput,
                                                                SizeScale.Full);
        [NodePin(PinType.In)]
        public TexturePin emission = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                SizeCastMode.ResizeToInput,
                                                                ColorCastMode.FitToInput,
                                                                SizeScale.Full);

        [NodePin(PinType.Out)]
        public TexturePin lightingResult = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.DefaultHDR, 0),
                                                                SizeCastMode.Fixed,
                                                                ColorCastMode.Fixed,
                                                                SizeScale.Full);


        static MaterialWithName lightingMat = new MaterialWithName("Hidden/DeferredLighting");

        public override void Excute(RenderContext context)
        {           
            var cam = context.RenderCamera;

            context.CmdBuffer.SetGlobalMatrix("_V", cam.worldToCameraMatrix);
            context.CmdBuffer.SetGlobalMatrix("_V_Inv", cam.cameraToWorldMatrix);
            context.CmdBuffer.SetGlobalMatrix("_VP_Inv", (GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * cam.worldToCameraMatrix).inverse);

            context.CmdBuffer.SetGlobalTexture("_DepthTex", depth.handle);
            context.CmdBuffer.SetGlobalTexture("_BaseColorTex", baseColor_roughness);
            context.CmdBuffer.SetGlobalTexture("_NormalTex", normal_metallic);
            context.CmdBuffer.SetGlobalTexture("_EmissionTex", emission);

            context.CmdBuffer.Blit(null, lightingResult, lightingMat, 0);
            context.Context.ExecuteCommandBuffer(context.CmdBuffer);
            context.CmdBuffer.Clear();
        }
    }
}