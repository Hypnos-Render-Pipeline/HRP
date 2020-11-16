using HypnosRenderPipeline.Tools;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class GBuffer : BaseRenderPass
    {
        [NodePin(PinType.InOut)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                                                    SizeCastMode.ResizeToInput,
                                                                    ColorCastMode.Fixed,
                                                                    SizeScale.Full);

        [NodePin(PinType.Out)]
        public TexturePin baseColor_roughness = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                    SizeCastMode.Fixed,
                                                                    ColorCastMode.Fixed,
                                                                    SizeScale.Full);
        [NodePin(PinType.Out)]
        public TexturePin normal_metallic = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
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
        public TexturePin motion = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.RGFloat, 0),
                                                                    SizeCastMode.Fixed,
                                                                    ColorCastMode.Fixed,
                                                                    SizeScale.Full);

        static MaterialWithName motionMat = new MaterialWithName("Hidden/CalculateMotion");

        public override void Excute(RenderContext context)
        {
            var cb = context.commandBuffer;
            cb.SetRenderTarget(new RenderTargetIdentifier[]{baseColor_roughness.handle, normal_metallic, emission, microAO }, depth);

            cb.ClearRenderTarget(!depth.connected, true, Color.clear);
            context.context.ExecuteCommandBuffer(context.commandBuffer);
            cb.Clear();

            var a = new DrawingSettings(new ShaderTagId("GBuffer_Equal"), new SortingSettings(context.camera));
            if (!depth.connected)
            {
                a = new DrawingSettings(new ShaderTagId("GBuffer_LEqual"), new SortingSettings(context.camera));
            }
            var b = FilteringSettings.defaultValue;
            b.renderQueueRange = RenderQueueRange.opaque;

            context.context.DrawRenderers(context.defaultCullingResult, ref a, ref b);

            if (motion.connected)
            {
                cb.SetGlobalTexture("_DepthTex", depth);
                cb.Blit(null, motion, motionMat, 0);
            }
        }
    }

}