using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class GBuffer : BaseRenderPass
    {
        [NodePin(PinType.InOut)]
        public TexturePin depth = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                                                        TexturePinDesc.SizeCastMode.ResizeToInput,
                                                                        TexturePinDesc.ColorCastMode.Fixed,
                                                                        TexturePinDesc.SizeScale.Full));

        [NodePin(PinType.Out)]
        public TexturePin baseColor_roughness = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                                       TexturePinDesc.SizeCastMode.Fixed,
                                                                                       TexturePinDesc.ColorCastMode.Fixed,
                                                                                       TexturePinDesc.SizeScale.Full));
        [NodePin(PinType.Out)]
        public TexturePin normal_metallic = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                                       TexturePinDesc.SizeCastMode.Fixed,
                                                                                       TexturePinDesc.ColorCastMode.Fixed,
                                                                                       TexturePinDesc.SizeScale.Full));
        [NodePin(PinType.Out)]
        public TexturePin emission = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                                       TexturePinDesc.SizeCastMode.Fixed,
                                                                                       TexturePinDesc.ColorCastMode.Fixed,
                                                                                       TexturePinDesc.SizeScale.Full));
        [NodePin(PinType.Out)]
        public TexturePin microAO = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                                       TexturePinDesc.SizeCastMode.Fixed,
                                                                                       TexturePinDesc.ColorCastMode.Fixed,
                                                                                       TexturePinDesc.SizeScale.Full));

        public override void Excute(RenderContext context)
        {
            context.CmdBuffer.SetRenderTarget(
                new[]{
                    (RenderTargetIdentifier)baseColor_roughness.handle,
                    (RenderTargetIdentifier)normal_metallic.handle,
                    (RenderTargetIdentifier)emission.handle,
                    (RenderTargetIdentifier)microAO.handle,
                }
                , depth.handle);

            context.CmdBuffer.ClearRenderTarget(false, true, Color.clear);
            context.Context.ExecuteCommandBuffer(context.CmdBuffer);
            context.CmdBuffer.Clear();

            ScriptableCullingParameters cullingParams;
            context.RenderCamera.TryGetCullingParameters(out cullingParams);
            var cullingResults = context.Context.Cull(ref cullingParams);

            var a = new DrawingSettings(new ShaderTagId("GBuffer"), new SortingSettings(context.RenderCamera));
            var b = FilteringSettings.defaultValue;

            context.Context.DrawRenderers(cullingResults, ref a, ref b);
        }
    }

}