using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline
{
    public static class CommandBufferExtension
    {
        public static void BlitSkybox(this CommandBuffer cb, int src, int dst, Material mat, int pass)
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

        public static void BlitSkybox(this CommandBuffer cb, RenderTexture src, int dst, Material mat, int pass)
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
    }
}
