using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderGraph
{
    /// <summary>
    /// Helper class allowing access to default resources (black or white texture, etc.) during render passes.
    /// </summary>
    public class FRGDefaultResources
    {
        bool m_IsValid;

        // We need to keep around a RTHandle version of default regular 2D textures since RenderGraph API is all RTHandle.
        FRTHandle m_BlackTexture2D;
        FRTHandle m_WhiteTexture2D;

        /// <summary>Default black 2D texture.</summary>
        public FRGTextureHandle blackTexture { get; private set; }
        /// <summary>Default white 2D texture.</summary>
        public FRGTextureHandle whiteTexture { get; private set; }
        /// <summary>Default clear color XR 2D texture.</summary>
        public FRGTextureHandle clearTextureXR { get; private set; }
        /// <summary>Default magenta XR 2D texture.</summary>
        public FRGTextureHandle magentaTextureXR { get; private set; }
        /// <summary>Default black XR 2D texture.</summary>
        public FRGTextureHandle blackTextureXR { get; private set; }
        /// <summary>Default black (UInt) XR 2D texture.</summary>
        public FRGTextureHandle blackUIntTextureXR { get; private set; }
        /// <summary>Default black XR 3D texture.</summary>
        public FRGTextureHandle blackTexture3DXR { get; private set; }
        /// <summary>Default white XR 2D texture.</summary>
        public FRGTextureHandle whiteTextureXR { get; private set; }

        internal FRGDefaultResources()
        {
            m_BlackTexture2D = FRTHandles.Alloc(Texture2D.blackTexture);
            m_WhiteTexture2D = FRTHandles.Alloc(Texture2D.whiteTexture);
        }

        internal void Cleanup()
        {
            m_BlackTexture2D.Release();
            m_WhiteTexture2D.Release();
        }

        internal void InitializeForRendering(FRenderGraph renderGraph)
        {
            if (!m_IsValid)
            {
                blackTexture = renderGraph.ImportTexture(m_BlackTexture2D);
                whiteTexture = renderGraph.ImportTexture(m_WhiteTexture2D);

                clearTextureXR = renderGraph.ImportTexture(FTextureXR.GetClearTexture());
                magentaTextureXR = renderGraph.ImportTexture(FTextureXR.GetMagentaTexture());
                blackTextureXR = renderGraph.ImportTexture(FTextureXR.GetBlackTexture());
                blackUIntTextureXR = renderGraph.ImportTexture(FTextureXR.GetBlackUIntTexture());
                blackTexture3DXR = renderGraph.ImportTexture(FTextureXR.GetBlackTexture3D());
                whiteTextureXR = renderGraph.ImportTexture(FTextureXR.GetWhiteTexture());

                m_IsValid = true;
            }
        }

        // Imported resources are cleared everytime the Render Graph is executed, so we need to know if that happens
        // so that we can re-import all default resources if needed.
        internal void Clear()
        {
            m_IsValid = false;
        }
    }
}

