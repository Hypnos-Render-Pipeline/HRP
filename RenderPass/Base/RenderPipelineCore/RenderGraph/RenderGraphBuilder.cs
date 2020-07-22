using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace HypnosRenderPipeline.RenderGraph
{
    public struct FRGBuilder : IDisposable
    {
        IRGRenderPass m_RenderPass;
        FRGResourceRegistry m_Resources;
        bool m_Disposed;

        #region Public Interface
        public FRGTextureHandle UseColorBuffer(in FRGTextureHandle input, int index)
        {
            CheckTemporalResource(input.handle);
            m_RenderPass.SetColorBuffer(input, index);
            return input;
        }

        public FRGTextureHandle UseDepthBuffer(in FRGTextureHandle input, EDepthAccess flags)
        {
            CheckTemporalResource(input.handle);
            m_RenderPass.SetDepthBuffer(input, flags);
            return input;
        }

        public FRGTextureHandle ReadTexture(in FRGTextureHandle input)
        {
            CheckTemporalResource(input.handle);
            m_RenderPass.AddResourceRead(input.handle);
            return input;
        }

        public FRGTextureHandle WriteTexture(in FRGTextureHandle input)
        {
            CheckTemporalResource(input.handle);
            m_RenderPass.AddResourceWrite(input.handle);
            return input;
        }

        public FRGTextureHandle CreateTemporalTexture(in FRGTextureDesc desc)
        {
            var result = m_Resources.CreateTexture(desc, 0, m_RenderPass.index);
            m_RenderPass.AddTemporalResource(result.handle);
            return result;
        }

        public FRGTextureHandle CreateTemporalTexture(in FRGTextureHandle texture)
        {
            var desc = m_Resources.GetTextureResourceDesc(texture.handle);
            var result = m_Resources.CreateTexture(desc, 0, m_RenderPass.index);
            m_RenderPass.AddTemporalResource(result.handle);
            return result;
        }

        public FRGRenderListHandle UseRendererList(in FRGRenderListHandle input)
        {
            m_RenderPass.UseRendererList(input);
            return input;
        }

        public FRGBufferHandle ReadBuffer(in FRGBufferHandle input)
        {
            CheckTemporalResource(input.handle);
            m_RenderPass.AddResourceRead(input.handle);
            return input;
        }

        public FRGBufferHandle WriteBuffer(in FRGBufferHandle input)
        {
            CheckTemporalResource(input.handle);
            m_RenderPass.AddResourceWrite(input.handle);
            return input;
        }

        public FRGBufferHandle CreateTemporalBuffer(in FRGBufferDesc desc)
        {
            var result = m_Resources.CreateBuffer(desc, m_RenderPass.index);
            m_RenderPass.AddTemporalResource(result.handle);
            return result;
        }

        public FRGBufferHandle CreateTemporalBuffer(in FRGBufferHandle buffer)
        {
            var desc = m_Resources.GetBufferResourceDesc(buffer.handle);
            var result = m_Resources.CreateBuffer(desc, m_RenderPass.index);
            m_RenderPass.AddTemporalResource(result.handle);
            return result;
        }

        public void SetRenderFunc<RGPassData>(FExecuteFunc<RGPassData> RenderFunc) where RGPassData : class, new()
        {
            ((FRGRenderPass<RGPassData>)m_RenderPass).ExecuteFunc = RenderFunc;
        }

        public void EnableAsyncCompute(bool value)
        {
            m_RenderPass.EnableAsyncCompute(value);
        }

        public void AllowPassCulling(bool value)
        {
            m_RenderPass.AllowPassCulling(value);
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

        #region Internal Interface
        internal FRGBuilder(IRGRenderPass renderPass, FRGResourceRegistry resources)
        {
            m_RenderPass = renderPass;
            m_Resources = resources;
            m_Disposed = false;
        }

        void Dispose(bool disposing)
        {
            if (m_Disposed)
                return;

            m_Disposed = true;
        }

        void CheckTemporalResource(in FRGResourceHandle res)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (res.IsValid())
            {
                int TemporalIndex = m_Resources.GetResourceTemporalIndex(res);
                if (TemporalIndex != -1 && TemporalIndex != m_RenderPass.index)
                {
                    throw new ArgumentException($"Trying to use a temporal texture (pass index {TemporalIndex}) in a different pass (pass index {m_RenderPass.index}.");
                }
            }
#endif
        }
        #endregion
    }
}
