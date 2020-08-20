using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderGraph
{
    public struct FRendererList
    {
        static readonly ShaderTagId s_EmptyName = new ShaderTagId("");
        public static readonly FRendererList nullRendererList = new FRendererList();
        public bool                 isValid { get; private set; }
        public CullingResults       cullingResult;
        public DrawingSettings      drawSettings;
        public FilteringSettings    filteringSettings;
        public RenderStateBlock?    stateBlock;

        public static FRendererList Create(in FRendererListDesc desc)
        {
            FRendererList newRenderList = new FRendererList();

            if (!desc.IsValid())
                return newRenderList;

            var sortingSettings = new SortingSettings(desc.camera)
            {
                criteria = desc.sortingCriteria
            };

            var drawSettings = new DrawingSettings(s_EmptyName, sortingSettings)
            {
                perObjectData = desc.rendererConfiguration
            };

            if (desc.passName != ShaderTagId.none) {
                Debug.Assert(desc.passNames == null);
                drawSettings.SetShaderPassName(0, desc.passName);
            } else {
                for (int i = 0; i < desc.passNames.Length; ++i)
                {
                    drawSettings.SetShaderPassName(i, desc.passNames[i]);
                }
            }

            if (desc.overrideMaterial != null) {
                drawSettings.overrideMaterial = desc.overrideMaterial;
                drawSettings.overrideMaterialPassIndex = desc.overrideMaterialPassIndex;
            }

            var filterSettings = new FilteringSettings(desc.renderQueueRange, desc.layerMask) {
                excludeMotionVectorObjects = desc.excludeObjectMotionVectors
            };

            newRenderList.isValid = true;
            newRenderList.cullingResult = desc.cullingResult;
            newRenderList.drawSettings = drawSettings;
            newRenderList.filteringSettings = filterSettings;
            newRenderList.stateBlock = desc.stateBlock;

            return newRenderList;
        }
    }

    public struct FRendererListDesc
    {
        public SortingCriteria sortingCriteria;
        public PerObjectData rendererConfiguration;
        public RenderQueueRange renderQueueRange;
        public RenderStateBlock? stateBlock;
        public Material overrideMaterial;
        public bool excludeObjectMotionVectors;
        public int layerMask;
        public int overrideMaterialPassIndex;
        internal CullingResults cullingResult { get; private set; }
        internal Camera camera { get; set; }
        internal ShaderTagId passName { get; private set; }
        internal ShaderTagId[] passNames { get; private set; }

        public FRendererListDesc(ShaderTagId passName, CullingResults cullingResult, Camera camera) : this()
        {
            this.passName = passName;
            this.passNames = null;
            this.cullingResult = cullingResult;
            this.camera = camera;
            this.layerMask = -1;
            this.overrideMaterialPassIndex = 0;
        }

        public FRendererListDesc(ShaderTagId[] passNames, CullingResults cullingResult, Camera camera) : this()
        {
            this.passNames = passNames;
            this.passName = ShaderTagId.none;
            this.cullingResult = cullingResult;
            this.camera = camera;
            this.layerMask = -1;
            this.overrideMaterialPassIndex = 0;
        }

        public bool IsValid()
        {
            if (camera == null || (passName == ShaderTagId.none && (passNames == null || passNames.Length == 0)))
                return false;

            return true;
        }
    }
}
