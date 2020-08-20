using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderGraph
{
    #region ResourcesPool
    public enum ETextureSizeMode
    {
        Explicit,
        Scale,
        Functor
    }

    public struct FRGTextureDesc
    {
        public ETextureSizeMode sizeMode;
        public int width;
        public int height;
        public int slices;
        public Vector2 scale;
        public FScaleFunc func;
        public EDepthBits depthBufferBits;
        public GraphicsFormat colorFormat;
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;
        public TextureDimension dimension;
        public bool enableRandomWrite;
        public bool useMipMap;
        public bool autoGenerateMips;
        public bool isShadowMap;
        public int anisoLevel;
        public float mipMapBias;
        public bool enableMSAA;
        public EMSAASamples msaaSamples;
        public bool bindTextureMS;
        public bool useDynamicScale;
        public RenderTextureMemoryless memoryless;
        public string name;
//#if UNITY_2020_2_OR_NEWER
//        public FastMemoryDesc fastMemoryDesc;
//#endif

        public bool clearBuffer;
        public Color clearColor;

        void InitDefaultValues(bool dynamicResolution, bool xrReady)
        {
            useDynamicScale = dynamicResolution;
            if (xrReady)
            {
                slices = FTextureXR.slices;
                dimension = FTextureXR.dimension;
            }
            else
            {
                slices = 1;
                dimension = TextureDimension.Tex2D;
            }
        }

        public FRGTextureDesc(int width, int height, bool dynamicResolution = false, bool xrReady = false) : this()
        {
            sizeMode = ETextureSizeMode.Explicit;
            this.width = width;
            this.height = height;
            msaaSamples = EMSAASamples.None;
            InitDefaultValues(dynamicResolution, xrReady);
        }

        public FRGTextureDesc(Vector2 scale, bool dynamicResolution = false, bool xrReady = false) : this()
        {
            sizeMode = ETextureSizeMode.Scale;
            this.scale = scale;
            msaaSamples = EMSAASamples.None;
            dimension = TextureDimension.Tex2D;
            InitDefaultValues(dynamicResolution, xrReady);
        }

        public FRGTextureDesc(FScaleFunc func, bool dynamicResolution = false, bool xrReady = false) : this()
        {
            sizeMode = ETextureSizeMode.Functor;
            this.func = func;
            msaaSamples = EMSAASamples.None;
            dimension = TextureDimension.Tex2D;
            InitDefaultValues(dynamicResolution, xrReady);
        }

        public FRGTextureDesc(FRGTextureDesc input)
        {
            this = input;
        }

        public override int GetHashCode()
        {
            int hashCode = 17;

            unchecked
            {
                switch (sizeMode)
                {
                    case ETextureSizeMode.Explicit:
                        hashCode = hashCode * 23 + width;
                        hashCode = hashCode * 23 + height;
                        hashCode = hashCode * 23 + (int)msaaSamples;
                        break;
                    case ETextureSizeMode.Functor:
                        if (func != null)
                            hashCode = hashCode * 23 + func.GetHashCode();
                        hashCode = hashCode * 23 + (enableMSAA ? 1 : 0);
                        break;
                    case ETextureSizeMode.Scale:
                        hashCode = hashCode * 23 + scale.x.GetHashCode();
                        hashCode = hashCode * 23 + scale.y.GetHashCode();
                        hashCode = hashCode * 23 + (enableMSAA ? 1 : 0);
                        break;
                }

                hashCode = hashCode * 23 + mipMapBias.GetHashCode();
                hashCode = hashCode * 23 + slices;
                hashCode = hashCode * 23 + (int)depthBufferBits;
                hashCode = hashCode * 23 + (int)colorFormat;
                hashCode = hashCode * 23 + (int)filterMode;
                hashCode = hashCode * 23 + (int)wrapMode;
                hashCode = hashCode * 23 + (int)dimension;
                hashCode = hashCode * 23 + (int)memoryless;
                hashCode = hashCode * 23 + anisoLevel;
                hashCode = hashCode * 23 + (enableRandomWrite ? 1 : 0);
                hashCode = hashCode * 23 + (useMipMap ? 1 : 0);
                hashCode = hashCode * 23 + (autoGenerateMips ? 1 : 0);
                hashCode = hashCode * 23 + (isShadowMap ? 1 : 0);
                hashCode = hashCode * 23 + (bindTextureMS ? 1 : 0);
                hashCode = hashCode * 23 + (useDynamicScale ? 1 : 0);
            }

            return hashCode;
        }
    }

    public struct FRGBufferDesc
    {
        public int count;
        public int stride;
        public ComputeBufferType type;
        public string name;

        public FRGBufferDesc(int count, int stride) : this()
        {
            this.count = count;
            this.stride = stride;
            type = ComputeBufferType.Default;
        }

        public FRGBufferDesc(int count, int stride, ComputeBufferType type) : this()
        {
            this.count = count;
            this.stride = stride;
            this.type = type;
        }

        public override int GetHashCode()
        {
            int hashCode = 17;

            hashCode = hashCode * 23 + count;
            hashCode = hashCode * 23 + stride;
            hashCode = hashCode * 23 + (int)type;

            return hashCode;
        }
    }

    abstract class FRGResourcePool<Type> where Type : class
    {
        protected Dictionary<int, List<(Type resource, int frameIndex)>> m_ResourcePool = new Dictionary<int, List<(Type resource, int frameIndex)>>();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        List<(int, Type)> m_FrameAllocatedResources = new List<(int, Type)>();
#endif

        protected static int s_CurrentFrameIndex;

        abstract protected void ReleaseInternalResource(Type res);
        abstract protected string GetResourceName(Type res);
        abstract protected string GetResourceTypeName();

        public void ReleaseResource(int hash, Type resource, int currentFrameIndex)
        {
            if (!m_ResourcePool.TryGetValue(hash, out var list))
            {
                list = new List<(Type resource, int frameIndex)>();
                m_ResourcePool.Add(hash, list);
            }

            list.Add((resource, currentFrameIndex));
        }

        public bool TryGetResource(int hashCode, out Type resource)
        {
            if (m_ResourcePool.TryGetValue(hashCode, out var list) && list.Count > 0)
            {
                resource = list[list.Count - 1].resource;
                list.RemoveAt(list.Count - 1); 
                return true;
            }

            resource = null;
            return false;
        }

        abstract public void PurgeUnusedResources(int currentFrameIndex);

        public void Cleanup()
        {
            foreach (var kvp in m_ResourcePool)
            {
                foreach (var res in kvp.Value)
                {
                    ReleaseInternalResource(res.resource);
                }
            }
        }

        public void RegisterFrameAllocation(int hash, Type value)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (hash != -1)
                m_FrameAllocatedResources.Add((hash, value));
#endif
        }

        public void UnregisterFrameAllocation(int hash, Type value)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (hash != -1)
                m_FrameAllocatedResources.Remove((hash, value));
#endif
        }

        public void CheckFrameAllocation(bool onException, int frameIndex)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (m_FrameAllocatedResources.Count != 0 && !onException)
            {
                string logMessage = $"RenderGraph: Not all resources of type {GetResourceTypeName()} were released. This can be caused by a resources being allocated but never read by any pass.";

                foreach (var value in m_FrameAllocatedResources)
                {
                    logMessage = $"{logMessage}\n\t{GetResourceName(value.Item2)}";
                    ReleaseResource(value.Item1, value.Item2, frameIndex);
                }

                Debug.LogWarning(logMessage);
            }

            m_FrameAllocatedResources.Clear();
#endif
        }
    }

    class FRGTexturePool : FRGResourcePool<FRTHandle>
    {
        protected override void ReleaseInternalResource(FRTHandle res)
        {
            res.Release();
        }

        protected override string GetResourceName(FRTHandle res)
        {
            return res.rt.name;
        }

        override protected string GetResourceTypeName()
        {
            return "Texture";
        }

        override public void PurgeUnusedResources(int currentFrameIndex)
        {
            s_CurrentFrameIndex = currentFrameIndex;

            foreach (var kvp in m_ResourcePool)
            {
                var list = kvp.Value;
                list.RemoveAll(obj =>
                {
                    if (obj.frameIndex < s_CurrentFrameIndex)
                    {
                        obj.resource.Release();
                        return true;
                    }
                    return false;
                });
            }
        }
    }

    class FRGBufferPool : FRGResourcePool<ComputeBuffer>
    {
        protected override void ReleaseInternalResource(ComputeBuffer res)
        {
            res.Release();
        }

        protected override string GetResourceName(ComputeBuffer res)
        {
            return "BufferNameNotAvailable";
        }

        override protected string GetResourceTypeName()
        {
            return "Buffer";
        }

        override public void PurgeUnusedResources(int currentFrameIndex)
        {
            s_CurrentFrameIndex = currentFrameIndex;

            foreach (var kvp in m_ResourcePool)
            {
                var list = kvp.Value;
                list.RemoveAll(obj =>
                {
                    if (obj.frameIndex < s_CurrentFrameIndex)
                    {
                        obj.resource.Release();
                        return true;
                    }
                    return false;
                });
            }
        }
    }
    #endregion


    #region Resources
    public enum FRGResourceType
    {
        Texture = 0,
        Buffer,
        Count
    }

    internal struct FRGResourceHandle
    {
        bool m_IsValid;

        public int index { get; private set; }
        public FRGResourceType type { get; private set; }
        public int iType { get { return (int)type; } }

        internal FRGResourceHandle(int value, FRGResourceType type)
        {
            index = value;
            this.type = type;
            m_IsValid = true;
        }

        public static implicit operator int(FRGResourceHandle handle) => handle.index;
        public bool IsValid() => m_IsValid;
    }

    public struct FRGTextureHandle
    {
        private static FRGTextureHandle s_NullHandle = new FRGTextureHandle();

        public static FRGTextureHandle nullHandle { get { return s_NullHandle; } }

        internal FRGResourceHandle handle;

        internal FRGTextureHandle(int handle) { this.handle = new FRGResourceHandle(handle, FRGResourceType.Texture); }

        //public static implicit operator FRTHandle(FRGTextureHandle texture) => texture.IsValid() ? FRGResourceRegistry.current.GetTexture(texture) : null;
        //public static implicit operator RenderTargetIdentifier(FRGTextureHandle texture) => texture.IsValid() ? FRGResourceRegistry.current.GetTexture(texture) : null;
        //public static implicit operator RenderTexture(FRGTextureHandle texture) => texture.IsValid() ? FRGResourceRegistry.current.GetTexture(texture) : null;
        public bool IsValid() => handle.IsValid();
    }

    public struct FRGBufferHandle
    {
        internal FRGResourceHandle handle;

        internal FRGBufferHandle(int handle) { this.handle = new FRGResourceHandle(handle, FRGResourceType.Buffer); }
        public static implicit operator ComputeBuffer(FRGBufferHandle bufferHandle) => bufferHandle.IsValid() ? RenderGraphResourcePool.current.GetBuffer(bufferHandle) : null;
        public bool IsValid() => handle.IsValid();
    }

    public struct FRGRenderListHandle
    {
        bool m_IsValid;
        internal int handle { get; private set; }
        internal FRGRenderListHandle(int handle) { this.handle = handle; m_IsValid = true; }
        public static implicit operator int(FRGRenderListHandle handle) { return handle.handle; }

        //public static implicit operator FRendererList(FRGRenderListHandle rendererList) => rendererList.IsValid() ? FRGResourceRegistry.current.GetRendererList(rendererList) : FRendererList.nullRendererList;
        public bool IsValid() => m_IsValid;
    }

    public class IRenderGraphResource
    {
        public bool imported;
        public int cachedHash;
        public int shaderProperty;
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
    
    class RenderGraphResource<DescType, ResType> : IRenderGraphResource where DescType : struct where ResType : class
    {
        public DescType desc;
        public ResType resource;

        protected RenderGraphResource()
        {

        }

        public override void Reset()
        {
            base.Reset();
            resource = null;
        }
    }

    class TextureResource : RenderGraphResource<FRGTextureDesc, FRTHandle>
    {
        public override string GetName()
        {
            return desc.name;
        }
    }

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


    public class RenderGraphResourcePool
    {
        static RenderGraphResourcePool m_CurrentRegistry;

        internal static RenderGraphResourcePool current
        {
            get {
                return m_CurrentRegistry;
            } set {
                m_CurrentRegistry = value;
            }
        }

        FRGBufferPool m_BufferPool = new FRGBufferPool();

        FRGTexturePool m_TexturePool = new FRGTexturePool();

        public List<IRenderGraphResource>[] m_Resources = new List<IRenderGraphResource>[(int)FRGResourceType.Count];


        public RenderGraphResourcePool()
        {
            for (int i = 0; i<(int)FRGResourceType.Count; ++i)
                m_Resources[i] = new List<IRenderGraphResource>();
        }

        internal void BeginRender()
        {
            current = this;
        }

        internal void EndRender()
        {
            current = null;
        }

        int AddNewResource<ResType>(List<IRenderGraphResource> resourceArray, out ResType outRes) where ResType : IRenderGraphResource, new()
        {
            int result = resourceArray.Count;
            resourceArray.Add(new ResType());

            outRes = resourceArray[result] as ResType;
            outRes.Reset();
            return result;
        }

        BufferResource GetBufferResource(in FRGResourceHandle handle)
        {
            return m_Resources[(int)FRGResourceType.Buffer][handle] as BufferResource;
        }

        internal ComputeBuffer GetBuffer(in FRGBufferHandle handle)
        {
            return GetBufferResource(handle.handle).resource;
        }

        public void GetBuffer(in FRGBufferDesc bufferDesc, out int bufferHandle)
        {
            int newHandle = AddNewResource(m_Resources[(int)FRGResourceType.Buffer], out BufferResource bufferResource);
            bufferResource.desc = bufferDesc;
            int hashCode = bufferResource.desc.GetHashCode();
            bufferHandle = newHandle;


            bufferResource.resource = null;
            if (!m_BufferPool.TryGetResource(hashCode, out bufferResource.resource))
            {
                bufferResource.resource = new ComputeBuffer(bufferResource.desc.count, bufferResource.desc.stride, bufferResource.desc.type);
                bufferResource.resource.name = bufferResource.desc.name;
                bufferResource.wasReleased = false;
            }
            bufferResource.cachedHash = hashCode;

            m_BufferPool.RegisterFrameAllocation(hashCode, bufferResource.resource);
        }

        public void ReleaseBuffer(in FRGBufferHandle bufferHandle)
        {
            BufferResource bufferResource = m_Resources[(int)FRGResourceType.Buffer][bufferHandle.handle] as BufferResource;

            m_BufferPool.ReleaseResource(bufferResource.cachedHash, bufferResource.resource, 0);
            m_BufferPool.UnregisterFrameAllocation(bufferResource.cachedHash, bufferResource.resource);
            bufferResource.cachedHash = -1;
            bufferResource.resource = null;
            bufferResource.wasReleased = true;
        }

        public void ClearUp()
        {
            m_BufferPool.Cleanup();
            m_TexturePool.Cleanup();
        }

    }
}
