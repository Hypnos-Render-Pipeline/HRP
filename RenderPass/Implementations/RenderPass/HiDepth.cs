using HypnosRenderPipeline.Tools;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class HiDepth : BaseRenderPass
    {
        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);


        [NodePin(PinType.Out)]
        public TexturePin hiZ;

        MaterialWithName hiZGen = new MaterialWithName("Hidden/HiZGeneration");

        public HiDepth()
        {
            hiZ = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.RFloat, 24, 7) { autoGenerateMips = false, useMipMap = true }, colorCastMode: ColorCastMode.Fixed, srcPin: depth);
        }

        public override void Excute(RenderContext context)
        {
            var cb = context.commandBuffer;
            int2 wh = new int2(depth.desc.basicDesc.width, depth.desc.basicDesc.height);

            cb.Blit(depth, hiZ);

            cb.SetGlobalTexture("_HiZDepth", hiZ);

            int tempDepth = Shader.PropertyToID("_TempDepth");

            for (int i = 1; i < 7; i++)
            {
                cb.GetTemporaryRT(tempDepth, wh.x >> i, wh.y >> i, 24, FilterMode.Point, RenderTextureFormat.RFloat);
                cb.SetGlobalInt("_MipLevel", i - 1);
                cb.Blit(null, tempDepth, hiZGen, 0);
                cb.CopyTexture(src: tempDepth, srcElement: 0, srcMip: 0, dst: hiZ, dstElement: 0, dstMip: i);
                cb.ReleaseTemporaryRT(tempDepth);
            }
        }
    }
}
