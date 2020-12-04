using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class PreZ : BaseRenderPass
    {
        [NodePin(PinType.InOut)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                                        SizeCastMode.ResizeToInput,
                                                        ColorCastMode.Fixed,
                                                        SizeScale.Full);

        public LayerMask mask = -1;

        public override void Execute(RenderContext context)
        {
            context.commandBuffer.SetRenderTarget(depth);
            if (!depth.connected)
            {
                context.commandBuffer.ClearRenderTarget(true, false, Color.black);
            }
            context.context.ExecuteCommandBuffer(context.commandBuffer);
            context.commandBuffer.Clear();

            var a = new DrawingSettings(new ShaderTagId("PreZ"), new SortingSettings(context.camera));
            var b = FilteringSettings.defaultValue;
            b.layerMask = mask.value;
            b.renderQueueRange = RenderQueueRange.opaque;

            context.context.DrawRenderers(context.defaultCullingResult, ref a, ref b);
        }
    }
}