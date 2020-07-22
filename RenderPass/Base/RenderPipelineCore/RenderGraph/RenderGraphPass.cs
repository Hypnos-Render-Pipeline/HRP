using System;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderGraph
{
    [DebuggerDisplay("RenderPass: {name} (Index:{index} Async:{enableAsyncCompute})")]
    abstract class IRGRenderPass
    {
        public FExecuteFunc<RGPassData> GetExecuteDelegate<RGPassData>() where RGPassData : class, new() => ( (FRGRenderPass<RGPassData>)this ).ExecuteFunc;

        public abstract void Execute(FRGContext renderGraphContext);
        public abstract void Release(FRGObjectPool pool);
        public abstract bool HasRenderFunc();

        public string           name { get; protected set; }
        public int              index { get; protected set; }
        public ProfilingSampler customSampler { get; protected set; }
        public bool             enableAsyncCompute { get; protected set; }
        public bool             allowPassCulling { get; protected set; }

        public FRGTextureHandle depthBuffer { get; protected set; }
        public FRGTextureHandle[]  colorBuffers { get; protected set; } = new FRGTextureHandle[FRenderGraph.kMaxMRTCount];
        public int              colorBufferMaxIndex { get; protected set; } = -1;
        public int              refCount { get; protected set; }

        public List<FRGResourceHandle>[] resourceReadLists = new List<FRGResourceHandle>[(int)FRGResourceType.Count];
        public List<FRGResourceHandle>[] resourceWriteLists = new List<FRGResourceHandle>[(int)FRGResourceType.Count];
        public List<FRGResourceHandle>[] temporalResourceList = new List<FRGResourceHandle>[(int)FRGResourceType.Count];

        public List<FRGRenderListHandle>     usedRendererListList = new List<FRGRenderListHandle>();

        public IRGRenderPass()
        {
            for (int i = 0; i < (int)FRGResourceType.Count; ++i)
            {
                resourceReadLists[i] = new List<FRGResourceHandle>();
                resourceWriteLists[i] = new List<FRGResourceHandle>();
                temporalResourceList[i] = new List<FRGResourceHandle>();
            }
        }

        public void Clear()
        {
            name = "";
            index = -1;
            customSampler = null;
            for (int i = 0; i < (int)FRGResourceType.Count; ++i)
            {
                resourceReadLists[i].Clear();
                resourceWriteLists[i].Clear();
                temporalResourceList[i].Clear();
            }

            usedRendererListList.Clear();
            enableAsyncCompute = false;
            allowPassCulling = true;
            refCount = 0;

            // Invalidate everything
            colorBufferMaxIndex = -1;
            depthBuffer = new FRGTextureHandle();
            for (int i = 0; i < FRenderGraph.kMaxMRTCount; ++i)
            {
                colorBuffers[i] = new FRGTextureHandle();
            }
        }

        public void AddResourceWrite(in FRGResourceHandle res)
        {
            resourceWriteLists[res.iType].Add(res);
        }

        public void AddResourceRead(in FRGResourceHandle res)
        {
            resourceReadLists[res.iType].Add(res);
        }

        public void AddTemporalResource(in FRGResourceHandle res)
        {
            temporalResourceList[res.iType].Add(res);
        }

        public void UseRendererList(FRGRenderListHandle rendererList)
        {
            usedRendererListList.Add(rendererList);
        }

        public void EnableAsyncCompute(bool value)
        {
            enableAsyncCompute = value;
        }

        public void AllowPassCulling(bool value)
        {
            allowPassCulling = value;
        }

        public void SetColorBuffer(FRGTextureHandle resource, int index)
        {
            Debug.Assert(index < FRenderGraph.kMaxMRTCount && index >= 0);
            colorBufferMaxIndex = Math.Max(colorBufferMaxIndex, index);
            colorBuffers[index] = resource;
            AddResourceWrite(resource.handle);
        }

        public void SetDepthBuffer(FRGTextureHandle resource, EDepthAccess flags)
        {
            depthBuffer = resource;
            if ((flags & EDepthAccess.Read) != 0)
                AddResourceRead(resource.handle);
            if ((flags & EDepthAccess.Write) != 0)
                AddResourceWrite(resource.handle);
        }
    }

    [DebuggerDisplay("RenderPass: {name} (Index:{index} Async:{enableAsyncCompute})")]
    internal sealed class FRGRenderPass<RGPassData> : IRGRenderPass where RGPassData : class, new()
    {
        internal RGPassData data;
        internal FExecuteFunc<RGPassData> ExecuteFunc;

        public override void Execute(FRGContext renderGraphContext)
        {
            GetExecuteDelegate<RGPassData>()(data, renderGraphContext);
        }

        public void Initialize(int passIndex, RGPassData passData, string passName, ProfilingSampler sampler)
        {
            Clear();
            index = passIndex;
            data = passData;
            name = passName;
            customSampler = sampler;
        }

        public override void Release(FRGObjectPool pool)
        {
            Clear();
            pool.Release(data);
            data = null;
            ExecuteFunc = null;

            // We need to do the release from here because we need the final type.
            pool.Release(this);
        }

        public override bool HasRenderFunc()
        {
            return ExecuteFunc != null;
        }
    }
}
