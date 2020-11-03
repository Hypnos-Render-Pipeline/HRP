using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class SkyBox : BaseRenderPass
    {
        [NodePin(PinType.In, true)]
        public TexturePin skyBox = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGBHalf) { dimension = TextureDimension.Cube });

        [NodePin(PinType.Out)]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.DefaultHDR, 0));

        static MaterialWithName skyBoxMat = new MaterialWithName("Hidden/SkyBox");

        public override void Excute(RenderContext context)
        {
            context.commandBuffer.Blit(skyBox, target, skyBoxMat);
        }
    }

}