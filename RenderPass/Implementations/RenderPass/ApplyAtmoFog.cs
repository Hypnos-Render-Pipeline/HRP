using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline.RenderPass
{
    public class ApplyAtmoFog : BaseRenderPass
    {
        [NodePin(PinType.In, true)]
        public AfterAtmo afterAtmo = new AfterAtmo();

        [NodePin(PinType.InOut)]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGBHalf));

        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        [NodePin(PinType.In)]
        public TexturePin skyIrradiance = new TexturePin(new RenderTextureDescriptor(32, 32, RenderTextureFormat.ARGBHalf) { dimension = TextureDimension.Cube }, sizeScale: SizeScale.Custom);

        [NodePin(PinType.In)]
        public TexturePin terrainShadowMap = new TexturePin(new RenderTextureDescriptor(2048, 2048, RenderTextureFormat.RFloat, 0));

        public override void Execute(RenderContext context)
        {
            if (afterAtmo.atmo != null)
            {
                var cb = context.commandBuffer;
                if (!target.connected)
                {
                    cb.SetRenderTarget(target);
                    cb.ClearRenderTarget(false, true, Color.black);
                }

                int tempColor = Shader.PropertyToID("TempColor");
                cb.GetTemporaryRT(tempColor, target.desc.basicDesc);
                cb.Blit(target, tempColor);
                afterAtmo.atmo.RenderFogToRT(cb, tempColor, depth, skyIrradiance, target);
                cb.ReleaseTemporaryRT(tempColor);
            }
        }
    }
}