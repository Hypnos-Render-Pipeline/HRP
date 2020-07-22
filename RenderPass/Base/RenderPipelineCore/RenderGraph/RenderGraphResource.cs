using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderGraph
{
    public enum FRGResourceType
    {
        Texture = 0,
        Buffer,
        Count
    }

    public struct FRGResourceHandle
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

    [DebuggerDisplay("Texture ({handle})")]
    public struct FRGTextureHandle
    {
        private static FRGTextureHandle s_NullHandle = new FRGTextureHandle();

        public static FRGTextureHandle nullHandle { get { return s_NullHandle; } }

        internal FRGResourceHandle handle;

        internal FRGTextureHandle(int handle) { this.handle = new FRGResourceHandle(handle, FRGResourceType.Texture); }

        public static implicit operator FRTHandle(FRGTextureHandle texture) => texture.IsValid() ? FRGResourceRegistry.current.GetTexture(texture) : null;
        public static implicit operator RenderTargetIdentifier(FRGTextureHandle texture) => texture.IsValid() ? FRGResourceRegistry.current.GetTexture(texture) : null;
        public static implicit operator RenderTexture(FRGTextureHandle texture) => texture.IsValid() ? FRGResourceRegistry.current.GetTexture(texture) : null;
        public bool IsValid() => handle.IsValid();
    }

    [DebuggerDisplay("Buffer ({handle})")]
    public struct FRGBufferHandle
    {
        internal FRGResourceHandle handle;

        internal FRGBufferHandle(int handle) { this.handle = new FRGResourceHandle(handle, FRGResourceType.Buffer); }
        public static implicit operator ComputeBuffer(FRGBufferHandle bufferHandle) => bufferHandle.IsValid() ? FRGResourceRegistry.current.GetBuffer(bufferHandle) : null;
        public bool IsValid() => handle.IsValid();
    }

    [DebuggerDisplay("RendererList ({handle})")]
    public struct FRGRenderListHandle
    {
        bool m_IsValid;
        internal int handle { get; private set; }
        internal FRGRenderListHandle(int handle) { this.handle = handle; m_IsValid = true; }
        public static implicit operator int(FRGRenderListHandle handle) { return handle.handle; }

        public static implicit operator FRendererList(FRGRenderListHandle rendererList) => rendererList.IsValid() ? FRGResourceRegistry.current.GetRendererList(rendererList) : FRendererList.nullRendererList;
        public bool IsValid() => m_IsValid;
    }
   public enum ETextureSizeMode
    {
        Explicit,
        Scale,
        Functor
    }

#if UNITY_2020_2_OR_NEWER
    public struct FastMemoryDesc
    {
        public bool inFastMemory;
        public FastMemoryFlags flags;
        public float residencyFraction;
    }
#endif

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
#if UNITY_2020_2_OR_NEWER
        public FastMemoryDesc fastMemoryDesc;
#endif

        public bool clearBuffer;
        public Color clearColor;

        void InitDefaultValues(bool dynamicResolution, bool xrReady)
        {
            useDynamicScale = dynamicResolution;
            if (xrReady) {
                slices = FTextureXR.slices;
                dimension = FTextureXR.dimension;
            } else {
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
}
