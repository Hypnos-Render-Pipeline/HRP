using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class PreZ : BaseRenderPass
    {
        [NodePin(PinType.InOut)]
        public TexturePin depth = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                                                        TexturePinDesc.SizeCastMode.ResizeToInput,
                                                                        TexturePinDesc.ColorCastMode.Fixed,
                                                                        TexturePinDesc.SizeScale.Full));

        public LayerMask mask = -1;


        [Tooltip("Should I clear depth before render")]
        public bool clearDepthFirst;

        public override void Excute(RenderContext context)
        {
            context.CmdBuffer.SetRenderTarget(depth.handle);
            if (clearDepthFirst)
            {
                context.CmdBuffer.ClearRenderTarget(true, false, Color.black);
            }
            context.Context.ExecuteCommandBuffer(context.CmdBuffer);
            context.CmdBuffer.Clear();

            ScriptableCullingParameters cullingParams;
            context.RenderCamera.TryGetCullingParameters(out cullingParams);
            var cullingResults = context.Context.Cull(ref cullingParams);

            var a = new DrawingSettings(new ShaderTagId("PreZ"), new SortingSettings(context.RenderCamera));
            var b = FilteringSettings.defaultValue;
            b.layerMask = mask.value;

            context.Context.DrawRenderers(cullingResults, ref a, ref b);
        }
    }
}