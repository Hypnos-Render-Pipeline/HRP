using UnityEngine;
using UnityEngine.Rendering;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline
{
    public static class TextureExtension
    {
        public static bool Equal(this RenderTextureDescriptor a, RenderTextureDescriptor b)
        {
            bool dif = false;
            dif |= a.autoGenerateMips != b.autoGenerateMips;
            dif |= a.bindMS != b.bindMS;
            dif |= a.colorFormat != b.colorFormat;
            dif |= a.depthBufferBits != b.depthBufferBits;
            dif |= a.dimension != b.dimension;
            dif |= a.enableRandomWrite != b.enableRandomWrite;
            dif |= a.flags != b.flags;
            dif |= a.graphicsFormat != b.graphicsFormat;
            dif |= a.height != b.height;
            dif |= a.memoryless != b.memoryless;
            dif |= a.mipCount != b.mipCount;
            dif |= a.msaaSamples != b.msaaSamples;
            dif |= a.shadowSamplingMode != b.shadowSamplingMode;
            dif |= a.sRGB != b.sRGB;
            dif |= a.stencilFormat != b.stencilFormat;
            dif |= a.useDynamicScale != b.useDynamicScale;
            dif |= a.useMipMap != b.useMipMap;
            dif |= a.volumeDepth != b.volumeDepth;
            dif |= a.vrUsage != b.vrUsage;
            dif |= a.width != b.width;
            return dif;
        }
    }
}
