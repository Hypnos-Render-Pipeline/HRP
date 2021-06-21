using HypnosRenderPipeline.Tools;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public enum E
    {
        a,b,c,d
    }
    public class GBuffer : BaseRenderPass
    {
        [NodePin(PinType.InOut)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                                                    SizeCastMode.ResizeToInput,
                                                                    ColorCastMode.Fixed,
                                                                    SizeScale.Full);

        [NodePin(PinType.Out)]
        public TexturePin diffuse = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                    SizeCastMode.Fixed,
                                                                    ColorCastMode.Fixed,
                                                                    SizeScale.Full);
        
        [NodePin(PinType.Out)]
        public TexturePin specular = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                    SizeCastMode.Fixed,
                                                    ColorCastMode.Fixed,
                                                    SizeScale.Full);
        
        [NodePin(PinType.Out)]
        public TexturePin normal = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                    SizeCastMode.Fixed,
                                                                    ColorCastMode.Fixed,
                                                                    SizeScale.Full);

        [NodePin(PinType.Out)]
        public TexturePin emission = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                    SizeCastMode.Fixed,
                                                                    ColorCastMode.Fixed,
                                                                    SizeScale.Full);
        [NodePin(PinType.Out)]
        public TexturePin microAO = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                    SizeCastMode.Fixed,
                                                                    ColorCastMode.Fixed,
                                                                    SizeScale.Full);

        [NodePin(PinType.Out)]
        public TexturePin index = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.R8, 0),
                                                            SizeCastMode.Fixed,
                                                            ColorCastMode.Fixed,
                                                            SizeScale.Full);

        [NodePin(PinType.Out)]
        public TexturePin motion = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.RGFloat, 0),
                                                                    SizeCastMode.Fixed,
                                                                    ColorCastMode.Fixed,
                                                                    SizeScale.Full);

        public float a = 1;
        public bool b = false;
        public Vector2 c = new Vector2(1.10973f, 2.0f);
        public E d = E.d;
        public LayerMask e = 10;
        public Color f = new Color(1, 2, 3, 0.22334f);

        static MaterialWithName motionMat = new MaterialWithName("Hidden/CalculateMotion");

        public override void Execute(RenderContext context)
        {
            var cb = context.commandBuffer;

            cb.SetRenderTarget(new RenderTargetIdentifier[]{diffuse.handle, specular, normal, emission, microAO }, depth);
            cb.ClearRenderTarget(!depth.connected, true, Color.clear);

            var a = new DrawingSettings(new ShaderTagId("GBuffer_Equal"), new SortingSettings(context.camera));
            if (!depth.connected)
            {
                a = new DrawingSettings(new ShaderTagId("GBuffer_LEqual"), new SortingSettings(context.camera));
            }
            a.enableInstancing = true;

            var b = FilteringSettings.defaultValue;
            b.renderQueueRange = new RenderQueueRange(2000, 2499);

            cb.DrawRenderers(context.defaultCullingResult, ref a, ref b);


            cb.SetRenderTarget(index);
            cb.ClearRenderTarget(false, true, Color.clear);
            cb.SetRenderTarget(new RenderTargetIdentifier[] { diffuse.handle, specular, normal, emission, microAO, index }, depth);

            a.SetShaderPassName(0, new ShaderTagId("Transparent"));
            b.renderQueueRange = new RenderQueueRange(2500, 2500);

            cb.DrawRenderers(context.defaultCullingResult, ref a, ref b);

            if (motion.connected)
            {
                cb.SetGlobalTexture("_DepthTex", depth);
                cb.Blit(null, motion, motionMat, 0);
            }

            cb.SetGlobalTexture("_DepthTex", depth);
            cb.SetGlobalTexture("_BaseColorTex", diffuse);
            cb.SetGlobalTexture("_SpecTex", specular);
            cb.SetGlobalTexture("_NormalTex", normal);
            cb.SetGlobalTexture("_EmissionTex", emission);
            cb.SetGlobalTexture("_AOTex", microAO);
            cb.SetGlobalTexture("_IndexTex", index);
            cb.SetGlobalTexture("_MotionTex", motion);
        }
    }

}