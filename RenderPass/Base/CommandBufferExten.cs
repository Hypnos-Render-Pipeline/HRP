using UnityEngine;
using UnityEngine.Rendering;
using HypnosRenderPipeline.Tools;
using Unity.Collections;
using System;

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

        public static void ClearSkybox(this CommandBuffer cb, RenderTargetIdentifier dst, bool clearDepth, bool clearColor, Color backgroundColor, float depth = 1.0f)
        {
            cb.SetRenderTarget(dst, 0, CubemapFace.PositiveX);
            cb.ClearRenderTarget(clearDepth, clearColor, backgroundColor, depth);

            cb.SetRenderTarget(dst, 0, CubemapFace.NegativeX);
            cb.ClearRenderTarget(clearDepth, clearColor, backgroundColor, depth);

            cb.SetRenderTarget(dst, 0, CubemapFace.PositiveY);
            cb.ClearRenderTarget(clearDepth, clearColor, backgroundColor, depth);

            cb.SetRenderTarget(dst, 0, CubemapFace.NegativeY);
            cb.ClearRenderTarget(clearDepth, clearColor, backgroundColor, depth);

            cb.SetRenderTarget(dst, 0, CubemapFace.PositiveZ);
            cb.ClearRenderTarget(clearDepth, clearColor, backgroundColor, depth);

            cb.SetRenderTarget(dst, 0, CubemapFace.NegativeZ);
            cb.ClearRenderTarget(clearDepth, clearColor, backgroundColor, depth);
        }

        public static void BlitDepth(this CommandBuffer cb, RenderTargetIdentifier src, RenderTargetIdentifier dst)
        {
            cb.Blit(src, dst, MaterialWithName.depthBlit);
        }

        static ScriptableRenderContext m_context;
        static bool m_GPUDriven;
        static string NotImp = "GPU Driven not implemented Exception";

        public static void SetupContext(ScriptableRenderContext context, bool GPUDriven = false)
        {
            m_context = context;
            m_GPUDriven = GPUDriven;
            if (GPUDriven)
            {
                throw new NotImplementedException(NotImp);
            }
        }


        public static void DrawRenderers(this CommandBuffer cb, CullingResults cullingResults, ref DrawingSettings drawingSettings, ref FilteringSettings filteringSettings, NativeArray<ShaderTagId> renderTypes, NativeArray<RenderStateBlock> stateBlocks)
        {
            if (!m_GPUDriven)
            {
                m_context.ExecuteCommandBuffer(cb);
                cb.Clear();
                m_context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings, renderTypes, stateBlocks);
            }
            else
            {
                throw new NotImplementedException(NotImp);
            }
        }
        public static void DrawRenderers(this CommandBuffer cb, CullingResults cullingResults, ref DrawingSettings drawingSettings, ref FilteringSettings filteringSettings, ref RenderStateBlock stateBlock)
        {
            if (!m_GPUDriven)
            {
                m_context.ExecuteCommandBuffer(cb);
                cb.Clear();
                m_context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings, ref stateBlock);
            }
            else
            {
                throw new NotImplementedException(NotImp);
            }
        }
        public static void DrawRenderers(this CommandBuffer cb, CullingResults cullingResults, ref DrawingSettings drawingSettings, ref FilteringSettings filteringSettings)
        {
            if (!m_GPUDriven)
            {
                m_context.ExecuteCommandBuffer(cb);
                cb.Clear();
                m_context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
            }
            else
            {
                throw new NotImplementedException(NotImp);
            }
        }
    }
}
