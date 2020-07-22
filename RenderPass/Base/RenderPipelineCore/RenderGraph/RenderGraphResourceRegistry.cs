using System;
using UnityEngine;
using System.Diagnostics;
using UnityEngine.Rendering;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;

namespace HypnosRenderPipeline.RenderGraph
{
    public class FRGResourceRegistry
    {
        static readonly ShaderTagId s_EmptyName = new ShaderTagId("");

        static FRGResourceRegistry m_CurrentRegistry;

        internal static FRGResourceRegistry current
        {
            get {
#if UNITY_EDITOR
                if (m_CurrentRegistry == null) {
                    throw new InvalidOperationException("Current Render Graph Resource Registry is not set. You are probably trying to cast a Render Graph handle to a resource outside of a Render Graph Pass.");
                }
#endif
                return m_CurrentRegistry;
            } set {
                m_CurrentRegistry = value; 
            }
        }

        class IRenderGraphResource
        {
            public bool imported;
            public int  cachedHash;
            public int  shaderProperty;
            public int temporalPassIndex;
            public bool wasReleased;

            public virtual void Reset()
            {
                imported = false;
                cachedHash = -1;
                shaderProperty = 0;
                temporalPassIndex = -1;
                wasReleased = false;
            }

            public virtual string GetName()
            {
                return "";
            }
        }

        #region Resources
        [DebuggerDisplay("Resource ({ResType}:{GetName()})")]
        class RenderGraphResource<DescType, ResType> : IRenderGraphResource where DescType : struct where ResType : class
        {
            public DescType desc;
            public ResType  resource;

            protected RenderGraphResource()
            {

            }

            public override void Reset()
            {
                base.Reset();
                resource = null;
            }
        }

        [DebuggerDisplay("TextureResource ({desc.name})")]
        class TextureResource : RenderGraphResource<FRGTextureDesc, FRTHandle>
        {
            public override string GetName()
            {
                return desc.name;
            }
        }

        [DebuggerDisplay("BufferResource ({desc.name})")]
        class BufferResource : RenderGraphResource<FRGBufferDesc, ComputeBuffer>
        {
            public override string GetName()
            {
                return desc.name;
            }
        }

        internal struct RendererListResource
        {
            public FRendererListDesc desc;
            public FRendererList rendererList;

            internal RendererListResource(in FRendererListDesc desc)
            {
                this.desc = desc;
                this.rendererList = new FRendererList(); // Invalid by default
            }
        }

        #endregion

        DynamicArray<IRenderGraphResource>[] m_Resources = new DynamicArray<IRenderGraphResource>[(int)FRGResourceType.Count];

        FRGBufferPool m_BufferPool = new FRGBufferPool();
        FRGTexturePool m_TexturePool = new FRGTexturePool();
        DynamicArray<RendererListResource> m_RendererListResources = new DynamicArray<RendererListResource>();
        FRGDebugParams m_RenderGraphDebug;
        FRGLogger m_Logger;
        int m_CurrentFrameIndex;

        FRTHandle m_CurrentBackbuffer;

        internal FRTHandle GetTexture(in FRGTextureHandle handle)
        {
            if (!handle.IsValid())
                return null;

            return GetTextureResource(handle.handle).resource;
        }

        internal FRendererList GetRendererList(in FRGRenderListHandle handle)
        {
            if (!handle.IsValid() || handle >= m_RendererListResources.size)
                return FRendererList.nullRendererList;

            return m_RendererListResources[handle].rendererList;
        }

        internal ComputeBuffer GetBuffer(in FRGBufferHandle handle)
        {
            if (!handle.IsValid())
                return null;

            return GetBufferResource(handle.handle).resource;
        }

        #region Internal Interface
        private FRGResourceRegistry()
        {

        }

        internal FRGResourceRegistry(bool supportMSAA, EMSAASamples initialSampleCount, FRGDebugParams renderGraphDebug, FRGLogger logger)
        {
            m_RenderGraphDebug = renderGraphDebug;
            m_Logger = logger;

            for (int i = 0; i < (int)FRGResourceType.Count; ++i)
                m_Resources[i] = new DynamicArray<IRenderGraphResource>();
        }

        ResType GetResource<DescType, ResType>(DynamicArray<IRenderGraphResource> resourceArray, int index)
            where DescType : struct
            where ResType : class
        {
            var res = resourceArray[index] as RenderGraphResource<DescType, ResType>;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (res.resource == null && !res.wasReleased)
                throw new InvalidOperationException(string.Format("Trying to access resource \"{0}\" that was never created. Check that it was written at least once before trying to get it.", res.GetName()));

            if (res.resource == null && res.wasReleased)
                throw new InvalidOperationException(string.Format("Trying to access resource \"{0}\" that was already released. Check that the last pass where it's read is after this one.", res.GetName()));
#endif
            return res.resource;
        }

        internal void BeginRender(int currentFrameIndex)
        {
            m_CurrentFrameIndex = currentFrameIndex;
            current = this;
        }

        internal void EndRender()
        {
            current = null;
        }

        void CheckHandleValidity(in FRGResourceHandle res)
        {
            var resources = m_Resources[res.iType];
            if (res.index >= resources.size)
                throw new ArgumentException($"Trying to access resource of type {res.type} with an invalid resource index {res.index}");
        }

        internal string GetResourceName(in FRGResourceHandle res)
        {
            CheckHandleValidity(res);
            return m_Resources[res.iType][res.index].GetName();
        }

        internal bool IsResourceImported(in FRGResourceHandle res)
        {
            CheckHandleValidity(res);
            return m_Resources[res.iType][res.index].imported;
        }

        internal int GetResourceTemporalIndex(in FRGResourceHandle res)
        {
            CheckHandleValidity(res);
            return m_Resources[res.iType][res.index].temporalPassIndex;
        }

        // Texture Creation/Import APIs are internal because creation should only go through RenderGraph
        internal FRGTextureHandle ImportTexture(FRTHandle rt, int shaderProperty = 0)
        {
            int newHandle = AddNewResource(m_Resources[(int)FRGResourceType.Texture], out TextureResource texResource);
            texResource.resource = rt;
            texResource.imported = true;
            texResource.shaderProperty = shaderProperty;

            return new FRGTextureHandle(newHandle);
        }

        internal FRGTextureHandle ImportBackbuffer(RenderTargetIdentifier rt)
        {
            if (m_CurrentBackbuffer != null)
                m_CurrentBackbuffer.SetTexture(rt);
            else
                m_CurrentBackbuffer = FRTHandles.Alloc(rt);

            int newHandle = AddNewResource(m_Resources[(int)FRGResourceType.Texture], out TextureResource texResource);
            texResource.resource = m_CurrentBackbuffer;
            texResource.imported = true;

            return new FRGTextureHandle(newHandle);
        }

        int AddNewResource<ResType>(DynamicArray<IRenderGraphResource> resourceArray, out ResType outRes) where ResType : IRenderGraphResource, new()
        {
            // In order to not create garbage, instead of using Add, we keep the content of the array while resizing and we just reset the existing ref (or create it if it's null).
            int result = resourceArray.size;
            resourceArray.Resize(resourceArray.size + 1, true);
            if (resourceArray[result] == null)
                resourceArray[result] = new ResType();

            outRes = resourceArray[result] as ResType;
            outRes.Reset();
            return result;
        }

        internal FRGTextureHandle CreateTexture(in FRGTextureDesc desc, int shaderProperty = 0, int temporalPassIndex = -1)
        {
            ValidateTextureDesc(desc);

            int newHandle = AddNewResource(m_Resources[(int)FRGResourceType.Texture], out TextureResource texResource);
            texResource.desc = desc;
            texResource.shaderProperty = shaderProperty;
            texResource.temporalPassIndex = temporalPassIndex;
            return new FRGTextureHandle(newHandle);
        }

        internal int GetTextureResourceCount()
        {
            return m_Resources[(int)FRGResourceType.Texture].size;
        }

        TextureResource GetTextureResource(in FRGResourceHandle handle)
        {
            return m_Resources[(int)FRGResourceType.Texture][handle] as TextureResource;
        }

        internal FRGTextureDesc GetTextureResourceDesc(in FRGResourceHandle handle)
        {
            return (m_Resources[(int)FRGResourceType.Texture][handle] as TextureResource).desc;
        }

        internal FRGRenderListHandle CreateRendererList(in FRendererListDesc desc)
        {
            ValidateRendererListDesc(desc);

            int newHandle = m_RendererListResources.Add(new RendererListResource(desc));
            return new FRGRenderListHandle(newHandle);
        }

        internal FRGBufferHandle ImportBuffer(ComputeBuffer computeBuffer)
        {
            int newHandle = AddNewResource(m_Resources[(int)FRGResourceType.Buffer], out BufferResource bufferResource);
            bufferResource.resource = computeBuffer;
            bufferResource.imported = true;

            return new FRGBufferHandle(newHandle);
        }

        internal FRGBufferHandle CreateBuffer(in FRGBufferDesc desc, int temporalPassIndex = -1)
        {
            ValidateBufferDesc(desc);

            int newHandle = AddNewResource(m_Resources[(int)FRGResourceType.Buffer], out BufferResource bufferResource);
            bufferResource.desc = desc;
            bufferResource.temporalPassIndex = temporalPassIndex;

            return new FRGBufferHandle(newHandle);
        }

        internal FRGBufferDesc GetBufferResourceDesc(in FRGResourceHandle handle)
        {
            return (m_Resources[(int)FRGResourceType.Buffer][handle] as BufferResource).desc;
        }

        internal int GetBufferResourceCount()
        {
            return m_Resources[(int)FRGResourceType.Buffer].size;
        }

        BufferResource GetBufferResource(in FRGResourceHandle handle)
        {
            return m_Resources[(int)FRGResourceType.Buffer][handle] as BufferResource;
        }

        internal void CreateAndClearTexture(FRGContext rgContext, int index)
        {
            var resource = m_Resources[(int)FRGResourceType.Texture][index] as TextureResource;

            if (!resource.imported)
            {
                var desc = resource.desc;
                int hashCode = desc.GetHashCode();

                if (resource.resource != null)
                    throw new InvalidOperationException(string.Format("Trying to create an already created texture ({0}). Texture was probably declared for writing more than once in the same pass.", resource.desc.name));

                resource.resource = null;
                if (!m_TexturePool.TryGetResource(hashCode, out resource.resource))
                {
                    string name = desc.name;
                    if (m_RenderGraphDebug.tagResourceNamesWithRG)
                        name = $"RenderGraph_{name}";

                    // Note: Name used here will be the one visible in the memory profiler so it means that whatever is the first pass that actually allocate the texture will set the name.
                    // TODO: Find a way to display name by pass.
                    switch (desc.sizeMode)
                    {
                        case ETextureSizeMode.Explicit:
                            resource.resource = FRTHandles.Alloc(desc.width, desc.height, desc.slices, desc.depthBufferBits, desc.colorFormat, desc.filterMode, desc.wrapMode, desc.dimension, desc.enableRandomWrite,
                            desc.useMipMap, desc.autoGenerateMips, desc.isShadowMap, desc.anisoLevel, desc.mipMapBias, desc.msaaSamples, desc.bindTextureMS, desc.useDynamicScale, desc.memoryless, desc.name);
                            break;
                        case ETextureSizeMode.Scale:
                            resource.resource = FRTHandles.Alloc(desc.scale, desc.slices, desc.depthBufferBits, desc.colorFormat, desc.filterMode, desc.wrapMode, desc.dimension, desc.enableRandomWrite,
                            desc.useMipMap, desc.autoGenerateMips, desc.isShadowMap, desc.anisoLevel, desc.mipMapBias, desc.enableMSAA, desc.bindTextureMS, desc.useDynamicScale, desc.memoryless, desc.name);
                            break;
                        case ETextureSizeMode.Functor:
                            resource.resource = FRTHandles.Alloc(desc.func, desc.slices, desc.depthBufferBits, desc.colorFormat, desc.filterMode, desc.wrapMode, desc.dimension, desc.enableRandomWrite,
                            desc.useMipMap, desc.autoGenerateMips, desc.isShadowMap, desc.anisoLevel, desc.mipMapBias, desc.enableMSAA, desc.bindTextureMS, desc.useDynamicScale, desc.memoryless, desc.name);
                            break;
                    }
                }

                //// Try to update name when re-using a texture.
                //// TODO RENDERGRAPH: Check if that actually works.
                //resource.rt.name = desc.name;

                resource.cachedHash = hashCode;

#if UNITY_2020_2_OR_NEWER
                var fastMemDesc = resource.desc.fastMemoryDesc;
                if(fastMemDesc.inFastMemory)
                {
                    resource.resource.SwitchToFastMemory(rgContext.cmd, fastMemDesc.residencyFraction, fastMemDesc.flags);
                }
#endif

                if (resource.desc.clearBuffer || m_RenderGraphDebug.clearRenderTargetsAtCreation)
                {
                    bool debugClear = m_RenderGraphDebug.clearRenderTargetsAtCreation && !resource.desc.clearBuffer;
                    var name = debugClear ? "RenderGraph: Clear Buffer (Debug)" : "RenderGraph: Clear Buffer";
                    using (new ProfilingScope(rgContext.cmd, ProfilingSampler.Get(FRGProfileId.RenderGraphClear)))
                    {
                        var clearFlag = resource.desc.depthBufferBits != EDepthBits.None ? ClearFlag.Depth : ClearFlag.Color;
                        var clearColor = debugClear ? Color.magenta : resource.desc.clearColor;
                        FCoreUtils.SetRenderTarget(rgContext.cmd, resource.resource, clearFlag, clearColor);
                    }
                }

                m_TexturePool.RegisterFrameAllocation(hashCode, resource.resource);
                LogTextureCreation(resource.resource, resource.desc.clearBuffer || m_RenderGraphDebug.clearRenderTargetsAtCreation);
            }
        }

        internal void CreateBuffer(FRGContext rgContext, int index)
        {
            var resource = m_Resources[(int)FRGResourceType.Buffer][index] as BufferResource;
            if (!resource.imported)
            {
                var desc = resource.desc;
                int hashCode = desc.GetHashCode();

                if (resource.resource != null)
                    throw new InvalidOperationException(string.Format("Trying to create an already created Compute Buffer ({0}). Buffer was probably declared for writing more than once in the same pass.", resource.desc.name));

                resource.resource = null;
                if (!m_BufferPool.TryGetResource(hashCode, out resource.resource))
                {
                    resource.resource = new ComputeBuffer(resource.desc.count, resource.desc.stride, resource.desc.type);
                    resource.resource.name = m_RenderGraphDebug.tagResourceNamesWithRG ? $"RenderGraph_{resource.desc.name}" : resource.desc.name;
                }
                resource.cachedHash = hashCode;

                m_BufferPool.RegisterFrameAllocation(hashCode, resource.resource);
                LogBufferCreation(resource.resource);
            }
        }

        void SetGlobalTextures(FRGContext rgContext, List<FRGResourceHandle> textures, bool bindDummyTexture)
        {
            foreach (var resource in textures)
            {
                var resourceDesc = GetTextureResource(resource);
                if (resourceDesc.shaderProperty != 0)
                {
                    if (resourceDesc.resource != null)
                    {
                        rgContext.cmd.SetGlobalTexture(resourceDesc.shaderProperty, bindDummyTexture ? FTextureXR.GetMagentaTexture() : resourceDesc.resource);
                    }
                }
            }
        }


        internal void PreRenderPassSetGlobalTextures(FRGContext rgContext, List<FRGResourceHandle> textures)
        {
            SetGlobalTextures(rgContext, textures, false);
        }

        internal void PostRenderPassUnbindGlobalTextures(FRGContext rgContext, List<FRGResourceHandle> textures)
        {
            SetGlobalTextures(rgContext, textures, true);
        }

        internal void ReleaseTexture(FRGContext rgContext, int index)
        {
            var resource = m_Resources[(int)FRGResourceType.Texture][index] as TextureResource;

            if (!resource.imported)
            {
                if (resource.resource == null)
                    throw new InvalidOperationException($"Tried to release a texture ({resource.desc.name}) that was never created. Check that there is at least one pass writing to it first.");

                if (m_RenderGraphDebug.clearRenderTargetsAtRelease)
                {
                    using (new ProfilingScope(rgContext.cmd, ProfilingSampler.Get(FRGProfileId.RenderGraphClearDebug)))
                    {
                        var clearFlag = resource.desc.depthBufferBits != EDepthBits.None ? ClearFlag.Depth : ClearFlag.Color;
                        // Not ideal to do new TextureHandle here but GetTexture is a public API and we rather have it take an explicit TextureHandle parameters.
                        // Everywhere else internally int is better because it allows us to share more code.
                        FCoreUtils.SetRenderTarget(rgContext.cmd, GetTexture(new FRGTextureHandle(index)), clearFlag, Color.magenta);
                    }
                }

                LogTextureRelease(resource.resource);
                m_TexturePool.ReleaseResource(resource.cachedHash, resource.resource, m_CurrentFrameIndex);
                m_TexturePool.UnregisterFrameAllocation(resource.cachedHash, resource.resource);
                resource.cachedHash = -1;
                resource.resource = null;
                resource.wasReleased = true;
            }
        }

        internal void ReleaseBuffer(FRGContext rgContext, int index)
        {
            var resource = m_Resources[(int)FRGResourceType.Buffer][index] as BufferResource;

            if (!resource.imported)
            {
                if (resource.resource == null)
                    throw new InvalidOperationException($"Tried to release a compute buffer ({resource.desc.name}) that was never created. Check that there is at least one pass writing to it first.");

                LogBufferRelease(resource.resource);
                m_BufferPool.ReleaseResource(resource.cachedHash, resource.resource, m_CurrentFrameIndex);
                m_BufferPool.UnregisterFrameAllocation(resource.cachedHash, resource.resource);
                resource.cachedHash = -1;
                resource.resource = null;
                resource.wasReleased = true;
            }
        }

        void ValidateTextureDesc(in FRGTextureDesc desc)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (desc.colorFormat == GraphicsFormat.None && desc.depthBufferBits == EDepthBits.None)
            {
                throw new ArgumentException("Texture was created with an invalid color format.");
            }

            if (desc.dimension == TextureDimension.None)
            {
                throw new ArgumentException("Texture was created with an invalid texture dimension.");
            }

            if (desc.slices == 0)
            {
                throw new ArgumentException("Texture was created with a slices parameter value of zero.");
            }

            if (desc.sizeMode == ETextureSizeMode.Explicit)
            {
                if (desc.width == 0 || desc.height == 0)
                    throw new ArgumentException("Texture using Explicit size mode was create with either width or height at zero.");
                if (desc.enableMSAA)
                    throw new ArgumentException("enableMSAA TextureDesc parameter is not supported for textures using Explicit size mode.");
            }

            if (desc.sizeMode == ETextureSizeMode.Scale || desc.sizeMode == ETextureSizeMode.Functor)
            {
                if (desc.msaaSamples != EMSAASamples.None)
                    throw new ArgumentException("msaaSamples TextureDesc parameter is not supported for textures using Scale or Functor size mode.");
            }
#endif
        }

        void ValidateRendererListDesc(in FRendererListDesc desc)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR

            if (desc.passName != ShaderTagId.none && desc.passNames != null
                || desc.passName == ShaderTagId.none && desc.passNames == null)
            {
                throw new ArgumentException("Renderer List creation descriptor must contain either a single passName or an array of passNames.");
            }

            if (desc.renderQueueRange.lowerBound == 0 && desc.renderQueueRange.upperBound == 0)
            {
                throw new ArgumentException("Renderer List creation descriptor must have a valid RenderQueueRange.");
            }

            if (desc.camera == null)
            {
                throw new ArgumentException("Renderer List creation descriptor must have a valid Camera.");
            }
#endif
        }

        void ValidateBufferDesc(in FRGBufferDesc desc)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // TODO RENDERGRAPH: Check actual condition on stride.
            if (desc.stride % 4 != 0)
            {
                throw new ArgumentException("Invalid Compute Buffer creation descriptor: Compute Buffer stride must be at least 4.");
            }
            if (desc.count == 0)
            {
                throw new ArgumentException("Invalid Compute Buffer creation descriptor: Compute Buffer count  must be non zero.");
            }
#endif
        }

        internal void CreateRendererLists(List<FRGRenderListHandle> rendererLists)
        {
            // For now we just create a simple structure
            // but when the proper API is available in trunk we'll kick off renderer lists creation jobs here.
            foreach (var rendererList in rendererLists)
            {
                ref var rendererListResource = ref m_RendererListResources[rendererList];
                ref var desc = ref rendererListResource.desc;
                FRendererList newRendererList = FRendererList.Create(desc);
                rendererListResource.rendererList = newRendererList;
            }
        }

        internal void Clear(bool onException)
        {
            LogResources();

            for (int i = 0; i < (int)FRGResourceType.Count; ++i)
                m_Resources[i].Clear();
            m_RendererListResources.Clear();

            m_TexturePool.CheckFrameAllocation(onException, m_CurrentFrameIndex);
            m_BufferPool.CheckFrameAllocation(onException, m_CurrentFrameIndex);
        }

        internal void PurgeUnusedResources()
        {
            // TODO RENDERGRAPH: Might not be ideal to purge stale resources every frame.
            // In case users enable/disable features along a level it might provoke performance spikes when things are reallocated...
            // Will be much better when we have actual resource aliasing and we can manage memory more efficiently.
            m_TexturePool.PurgeUnusedResources(m_CurrentFrameIndex);
            m_BufferPool.PurgeUnusedResources(m_CurrentFrameIndex);
        }

        internal void Cleanup()
        {
            m_TexturePool.Cleanup();
            m_BufferPool.Cleanup();
        }

        void LogTextureCreation(FRTHandle rt, bool cleared)
        {
            if (m_RenderGraphDebug.logFrameInformation)
            {
                m_Logger.LogLine($"Created Texture: {rt.rt.name} (Cleared: {cleared})");
            }
        }

        void LogTextureRelease(FRTHandle rt)
        {
            if (m_RenderGraphDebug.logFrameInformation)
            {
                m_Logger.LogLine($"Released Texture: {rt.rt.name}");
            }
        }

        void LogBufferCreation(ComputeBuffer buffer)
        {
            if (m_RenderGraphDebug.logFrameInformation)
            {
                m_Logger.LogLine($"Created Buffer: {buffer}");
            }
        }

        void LogBufferRelease(ComputeBuffer buffer)
        {
            if (m_RenderGraphDebug.logFrameInformation)
            {
                m_Logger.LogLine($"Released Buffer: {buffer}");
            }
        }

        void LogResources()
        {
            if (m_RenderGraphDebug.logResources)
            {
                m_Logger.LogLine("==== Allocated Resources ====\n");

                m_TexturePool.LogResources(m_Logger);
                m_BufferPool.LogResources(m_Logger);
            }
        }

        #endregion
    }
}
