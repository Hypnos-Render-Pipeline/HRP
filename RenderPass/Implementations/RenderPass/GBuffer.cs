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

        public override void Excute(RenderContext context)
        {
            context.commandBuffer.SetRenderTarget(new RenderTargetIdentifier[]{baseColor_roughness.handle, normal_metallic, emission, microAO }, depth);

            context.commandBuffer.ClearRenderTarget(!depth.connected, true, Color.clear);
            context.context.ExecuteCommandBuffer(context.commandBuffer);
            context.commandBuffer.Clear();

            var a = new DrawingSettings(new ShaderTagId("GBuffer_Equal"), new SortingSettings(context.camera));
            if (!depth.connected)
            {
                a = new DrawingSettings(new ShaderTagId("GBuffer_LEqual"), new SortingSettings(context.camera));
            }
            var b = FilteringSettings.defaultValue;
            b.renderQueueRange = RenderQueueRange.opaque;

            context.context.DrawRenderers(context.defaultCullingResult, ref a, ref b);
        }
    }

}