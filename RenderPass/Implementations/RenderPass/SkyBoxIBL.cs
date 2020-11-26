using UnityEngine;
using UnityEngine.Rendering;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline.RenderPass
{
    public class SkyBoxIBL : BaseRenderPass
    {
        [NodePin(PinType.In, true)]
        public TexturePin skyBox = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGBHalf) { dimension = TextureDimension.Cube });

        [NodePin(PinType.InOut, true)]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGBHalf));

        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        [NodePin(PinType.In, true)]
        public TexturePin diffuse = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin specular = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin normal = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In)]
        public TexturePin ao = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.Out)]
        public TexturePin irradiance = new TexturePin(new RenderTextureDescriptor(32, 32, RenderTextureFormat.ARGBHalf) { dimension = TextureDimension.Cube }, sizeScale: SizeScale.Custom);

        static MaterialWithName filterMat = new MaterialWithName("Hidden/FilterSkybox");
        static MaterialWithName iblMat = new MaterialWithName("Hidden/IBL");

        public override void Excute(RenderContext context)
        {
            var cb = context.commandBuffer;

            cb.BlitSkybox(skyBox, irradiance, filterMat, 0);

            cb.Blit(irradiance, target, iblMat, 0);
        }
    }
}