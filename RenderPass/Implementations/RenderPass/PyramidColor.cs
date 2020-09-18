using Unity.Mathematics;
using UnityEngine;

namespace HypnosRenderPipeline.RenderPass
{
    public class PyramidColor : BaseRenderPass
    {
        [NodePin(PinType.InOut, true)]
        public TexturePin filterTarget = new TexturePin(new RenderTextureDescriptor(1, 1));

        public PyramidColor()
        {

        }

        public override void Excute(RenderContext context)
        {

        }
    }
}