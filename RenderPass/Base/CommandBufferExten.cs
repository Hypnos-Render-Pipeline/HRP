using UnityEngine;
using UnityEngine.Rendering;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline
{
    public static class CommandBufferExtension
    {
        public static void BlitSkybox(this CommandBuffer cb, RenderTargetIdentifier src, RenderTargetIdentifier dst, Material mat, int pass)
        {
            cb.SetRenderTarget(dst, 0, CubemapFace.PositiveX);
            cb.SetGlobalInt("_Slice", 0);
            cb.Blit(src, BuiltinRenderTextureType.CurrentActive, mat, pass);

            cb.SetRenderTarget(dst, 0, CubemapFace.NegativeX);
            cb.SetGlobalInt("_Slice", 1);
            cb.Blit(src, BuiltinRenderTextureType.CurrentActive, mat, pass);

            cb.SetRenderTarget(dst, 0, CubemapFace.PositiveY);
            cb.SetGlobalInt("_Slice", 2);
            cb.Blit(src, BuiltinRenderTextureType.CurrentActive, mat, pass);

            cb.SetRenderTarget(dst, 0, CubemapFace.NegativeY);
            cb.SetGlobalInt("_Slice", 3);
            cb.Blit(src, BuiltinRenderTextureType.CurrentActive, mat, pass);

            cb.SetRenderTarget(dst, 0, CubemapFace.PositiveZ);
            cb.SetGlobalInt("_Slice", 4);
            cb.Blit(src, BuiltinRenderTextureType.CurrentActive, mat, pass);

            cb.SetRenderTarget(dst, 0, CubemapFace.NegativeZ);
            cb.SetGlobalInt("_Slice", 5);
            cb.Blit(src, BuiltinRenderTextureType.CurrentActive, mat, pass);
        }

        public static void BlitDepth(this CommandBuffer cb, RenderTargetIdentifier src, RenderTargetIdentifier dst)
        {
            cb.Blit(src, dst, MaterialWithName.depthBlit);
        }
    }
}
