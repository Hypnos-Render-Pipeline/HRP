using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Legacy;
using UnityEditor.ShaderGraph.Serialization;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System.Linq;
using HypnosRenderPipeline.RenderGraph;

namespace HypnosRenderPipeline.VFX.ShaderGraph
{
    internal enum ShaderID
    {
        Lit,
        Unlit,
    }

    sealed class HRPTarget : Target, ILegacyTarget
    {
        // Constants
        static readonly GUID kSourceCodeGuid = new GUID("8db9e66ec511f37489a46e40280c67ba");

        // SubTarget
        List<SubTarget> m_SubTargets;
        List<string> m_SubTargetNames;
        int activeSubTargetIndex => m_SubTargets.IndexOf(m_ActiveSubTarget);

        // View
        PopupField<string> m_SubTargetField;
        TextField m_CustomGUIField;

        [SerializeField]
        JsonData<SubTarget> m_ActiveSubTarget;
        
        [SerializeField]
        string m_CustomEditorGUI;

        internal override bool ignoreCustomInterpolators => false;
        internal override int padCustomInterpolatorLimit => 8;

        public override bool IsNodeAllowedByTarget(Type nodeType)
        {
            SRPFilterAttribute srpFilter = NodeClassCache.GetAttributeOnNodeType<SRPFilterAttribute>(nodeType);
            bool worksWithThisSrp = srpFilter == null || srpFilter.srpTypes.Contains(typeof(HypnosRenderPipeline));
            return worksWithThisSrp && base.IsNodeAllowedByTarget(nodeType);
        }

        public HRPTarget()
        {
            displayName = "HRP";
            m_SubTargets = TargetUtils.GetSubTargets(this);
            m_SubTargetNames = m_SubTargets.Select(x => x.displayName).ToList();

            TargetUtils.ProcessSubTargetList(ref m_ActiveSubTarget, ref m_SubTargets);
        }

        public string customEditorGUI
        {
            get => m_CustomEditorGUI;
            set => m_CustomEditorGUI = value;
        }

        public override bool IsActive()
        {
            if (m_ActiveSubTarget.value == null)
                return false;

            bool isHDRenderPipeline = GraphicsSettings.currentRenderPipeline is HypnosRenderPipelineAsset;
            return isHDRenderPipeline && m_ActiveSubTarget.value.IsActive();
        }

        public override void Setup(ref TargetSetupContext context)
        {
            // Setup the Target
            context.AddAssetDependency(kSourceCodeGuid, AssetCollection.Flags.SourceDependency);

            // Process SubTargets
            TargetUtils.ProcessSubTargetList(ref m_ActiveSubTarget, ref m_SubTargets);
            if (m_ActiveSubTarget.value == null)
                return;

            // Override EditorGUI (replaces the HDRP material editor by a custom one)
            if (!string.IsNullOrEmpty(m_CustomEditorGUI))
                context.AddCustomEditorForRenderPipeline(m_CustomEditorGUI, typeof(HypnosRenderPipelineAsset));

            // Setup the active SubTarget
            m_ActiveSubTarget.value.target = this;
            m_ActiveSubTarget.value.Setup(ref context);
        }

        public override void GetFields(ref TargetFieldContext context)
        {
            var descs = context.blocks.Select(x => x.descriptor);
            // Stages
            context.AddField(Fields.GraphVertex, descs.Contains(BlockFields.VertexDescription.Position) ||
                descs.Contains(BlockFields.VertexDescription.Normal) ||
                descs.Contains(BlockFields.VertexDescription.Tangent));
            context.AddField(Fields.GraphPixel);

            //// SubTarget
            m_ActiveSubTarget.value.GetFields(ref context);
        }

        public override void GetActiveBlocks(ref TargetActiveBlockContext context)
        {
            m_ActiveSubTarget.value.GetActiveBlocks(ref context);
        }

        public override void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange, Action<String> registerUndo)
        {
            if (m_ActiveSubTarget.value == null)
                return;

            context.globalIndentLevel++;

            // Core properties
            m_SubTargetField = new PopupField<string>(m_SubTargetNames, activeSubTargetIndex);
            context.AddProperty("Material", m_SubTargetField, (evt) =>
            {
                if (Equals(activeSubTargetIndex, m_SubTargetField.index))
                    return;

                m_ActiveSubTarget = m_SubTargets[m_SubTargetField.index];
                ProcessSubTargetDatas(m_ActiveSubTarget.value);
                onChange();
            });

            // SubTarget properties
            m_ActiveSubTarget.value.GetPropertiesGUI(ref context, onChange, registerUndo);

            // Custom Editor GUI
            m_CustomGUIField = new TextField("") { value = m_CustomEditorGUI };
            m_CustomGUIField.RegisterCallback<FocusOutEvent>(s =>
            {
                if (Equals(m_CustomEditorGUI, m_CustomGUIField.value))
                    return;

                m_CustomEditorGUI = m_CustomGUIField.value;
                onChange();
            });
            context.AddProperty("Custom Editor GUI", m_CustomGUIField, (evt) => { });

            context.globalIndentLevel--;
        }

        public override void CollectShaderProperties(PropertyCollector collector, GenerationMode generationMode)
        {
            // SubTarget
            m_ActiveSubTarget.value.CollectShaderProperties(collector, generationMode);
        }

        public override void ProcessPreviewMaterial(Material material)
        {
            // SubTarget
            m_ActiveSubTarget.value.ProcessPreviewMaterial(material);
        }

        public override object saveContext => m_ActiveSubTarget.value?.saveContext;

        // IHasMetaData
        public string identifier
        {
            get
            {
                if (m_ActiveSubTarget.value is IHasMetadata subTargetHasMetaData)
                    return subTargetHasMetaData.identifier;

                return null;
            }
        }

        public bool TrySetActiveSubTarget(Type subTargetType)
        {
            if (!subTargetType.IsSubclassOf(typeof(SubTarget)))
                return false;

            foreach (var subTarget in m_SubTargets)
            {
                if (subTarget.GetType().Equals(subTargetType))
                {
                    m_ActiveSubTarget = subTarget;
                    ProcessSubTargetDatas(m_ActiveSubTarget);
                    return true;
                }
            }

            return false;
        }

        void ProcessSubTargetDatas(SubTarget subTarget)
        {
            //var typeCollection = TypeCache.GetTypesDerivedFrom<HDTargetData>();
            //foreach (var type in typeCollection)
            //{
            //    // Data requirement interfaces need generic type arguments
            //    // Therefore we need to use reflections to call the method
            //    var methodInfo = typeof(HDTarget).GetMethod("SetDataOnSubTarget");
            //    var genericMethodInfo = methodInfo.MakeGenericMethod(type);
            //    genericMethodInfo.Invoke(this, new object[] { subTarget });
            //}
        }

        public override void OnBeforeSerialize()
        {
        }

        public bool TryUpgradeFromMasterNode(IMasterNode1 masterNode, out Dictionary<BlockFieldDescriptor, int> blockMap)
        {
            blockMap = null;

            // Process SubTargets
            foreach (var subTarget in m_SubTargets)
            {
                if (!(subTarget is ILegacyTarget legacySubTarget))
                    continue;

                ProcessSubTargetDatas(subTarget);
                subTarget.target = this;

                if (legacySubTarget.TryUpgradeFromMasterNode(masterNode, out blockMap))
                {
                    m_ActiveSubTarget = subTarget;
                    return true;
                }
            }

            return false;
        }

        public override bool WorksWithSRP(RenderPipelineAsset scriptableRenderPipeline)
        {
            return scriptableRenderPipeline?.GetType() == typeof(HypnosRenderPipeline);
        }
    }







    abstract class HRPSubTarget : SubTarget<HRPTarget>
    {
        protected bool m_MigrateFromOldCrossPipelineSG; // Use only for the migration to shader stack architecture
        protected bool m_MigrateFromOldSG; // Use only for the migration from early shader stack architecture to recent one

        protected virtual int ComputeMaterialNeedsUpdateHash() => 0;

        public override bool IsActive() => true;

        protected abstract ShaderID shaderID { get; }
        protected abstract string customInspector { get; }
        protected abstract GUID subTargetAssetGuid { get; }
        protected abstract string renderType { get; }
        protected abstract string renderQueue { get; }
        protected virtual string templatePath => $"{Tools.PathDefine.path}VFX/Editor/SG/Templetes/ShaderPass.template";

        static string[] passTemplateMaterialDirectories = new string[]
        {
            $"{Tools.PathDefine.path}VFX/Editor/SG/Templetes/",
            $"{Tools.PathDefine.path}Lighting/Runtime/Resources/Shaders/Includes/",
        };

        protected virtual string[] templateMaterialDirectories => passTemplateMaterialDirectories;
        protected abstract FieldDescriptor subShaderField { get; }

        public virtual string identifier => GetType().Name;

        static readonly GUID kSourceCodeGuid = new GUID("8db9e66ec511f37489a46e40280c67ba");

        public override void Setup(ref TargetSetupContext context)
        {
            context.AddAssetDependency(kSourceCodeGuid, AssetCollection.Flags.SourceDependency);
            context.AddAssetDependency(subTargetAssetGuid, AssetCollection.Flags.SourceDependency);
            if (!context.HasCustomEditorForRenderPipeline(typeof(HypnosRenderPipelineAsset)))
                if (!string.IsNullOrEmpty(customInspector))
                    context.AddCustomEditorForRenderPipeline(customInspector, typeof(HypnosRenderPipelineAsset));

            OnBeforeSerialize();

            foreach (var subShader in EnumerateSubShaders())
            {
                // patch render type and render queue from pass declaration:
                var patchedSubShader = subShader;
                patchedSubShader.pipelineTag = "HypnosRenderPipeline";
                patchedSubShader.renderQueue = renderQueue;
                patchedSubShader.renderType = "Geometry";
                context.AddSubShader(patchedSubShader);
            }
        }

        protected SubShaderDescriptor PostProcessSubShader(SubShaderDescriptor subShaderDescriptor)
        {
            if (String.IsNullOrEmpty(subShaderDescriptor.pipelineTag))
                subShaderDescriptor.pipelineTag = "HRP";

            var passes = subShaderDescriptor.passes.ToArray();
            PassCollection finalPasses = new PassCollection();
            for (int i = 0; i < passes.Length; i++)
            {
                var passDescriptor = passes[i].descriptor;
                if (string.IsNullOrEmpty(passDescriptor.passTemplatePath))
                    passDescriptor.passTemplatePath = templatePath;
                passDescriptor.sharedTemplateDirectories = templateMaterialDirectories;

                // Add the subShader to enable fields that depends on it
                var originalRequireFields = passDescriptor.requiredFields;
                // Duplicate require fields to avoid unwanted shared list modification
                passDescriptor.requiredFields = new FieldCollection();
                if (originalRequireFields != null)
                    foreach (var field in originalRequireFields)
                        passDescriptor.requiredFields.Add(field.field);
                passDescriptor.requiredFields.Add(subShaderField);

                IncludeCollection totalInclude = new IncludeCollection();
                totalInclude.Add(CoreIncludes.CorePregraph);
                totalInclude.Add(passDescriptor.includes);
                passDescriptor.includes = totalInclude;

                // Replace valid pixel blocks by automatic thing so we don't have to write them
                var tmpCtx = new TargetActiveBlockContext(new List<BlockFieldDescriptor>(), passDescriptor);
                GetActiveBlocks(ref tmpCtx);
                if (passDescriptor.validPixelBlocks == null)
                    passDescriptor.validPixelBlocks = tmpCtx.activeBlocks.Where(b => b.shaderStage == ShaderStage.Fragment).ToArray();
                if (passDescriptor.validVertexBlocks == null)
                    passDescriptor.validVertexBlocks = tmpCtx.activeBlocks.Where(b => b.shaderStage == ShaderStage.Vertex).ToArray();

                // Add keywords from subshaders:
                passDescriptor.keywords = passDescriptor.keywords == null ? new KeywordCollection() : new KeywordCollection { passDescriptor.keywords }; // Duplicate keywords to avoid side effects (static list modification)
                passDescriptor.defines = passDescriptor.defines == null ? new DefineCollection() : new DefineCollection { passDescriptor.defines }; // Duplicate defines to avoid side effects (static list modification)
                CollectPassKeywords(ref passDescriptor);

                // Set default values for HDRP "surface" passes:
                if (passDescriptor.structs == null)
                    passDescriptor.structs = new StructCollection();
                if (passDescriptor.fieldDependencies == null)
                    passDescriptor.fieldDependencies = new DependencyCollection();

                finalPasses.Add(passDescriptor, passes[i].fieldConditions);
            }

            subShaderDescriptor.passes = finalPasses;

            return subShaderDescriptor;
        }

        protected virtual void CollectPassKeywords(ref PassDescriptor pass) { }

        public override void GetFields(ref TargetFieldContext context)
        {
            // Common properties between all HD master nodes
            // Dots
            //context.AddField(HDFields.DotsInstancing, systemData.dotsInstancing);
        }

        protected abstract IEnumerable<SubShaderDescriptor> EnumerateSubShaders();

        public override void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange, Action<String> registerUndo)
        {
            var gui = new SubTargetPropertiesGUI(context, onChange, registerUndo);
            AddInspectorPropertyBlocks(gui);
            context.Add(gui);
        }
        protected abstract void AddInspectorPropertyBlocks(SubTargetPropertiesGUI blockList);
    }

    class SubTargetPropertiesGUI : VisualElement
    {
        TargetPropertyGUIContext context;
        Action onChange;
        Action<String> registerUndo;

        public List<SubTargetPropertyBlock> uiBlocks = new List<SubTargetPropertyBlock>();

        public SubTargetPropertiesGUI(TargetPropertyGUIContext context, Action onChange, Action<String> registerUndo)
        {
            this.context = context;
            this.onChange = onChange;
            this.registerUndo = registerUndo;
        }

        public void AddPropertyBlock(SubTargetPropertyBlock block)
        {
            block.Initialize(context, onChange, registerUndo);
            block.CreatePropertyGUIWithHeader();
            Add(block);
        }
    }

    abstract class SubTargetPropertyBlock : VisualElement
    {
        // Null/Empty means no title
        protected virtual string title => null;

        protected TargetPropertyGUIContext context;
        protected Action onChange;
        protected Action<String> registerUndo;

        internal void Initialize(TargetPropertyGUIContext context, Action onChange, Action<String> registerUndo)
        {
            this.context = context;
            this.onChange = onChange;
            this.registerUndo = registerUndo;
        }

        // Utility function to create UIElement fields:
        protected void AddProperty<Data>(string displayName, Func<Data> getter, Action<Data> setter, int indentLevel = 0)
            => AddProperty<Data>(new GUIContent(displayName), getter, setter, indentLevel);

        protected void AddProperty<Data>(GUIContent displayName, Func<Data> getter, Action<Data> setter, int indentLevel = 0)
        {
            // Create UIElement from type:
            BaseField<Data> elem = null;
            BaseField<Enum> elemEnum = null;

            switch (getter())
            {
                case bool b: elem = new Toggle { value = b, tooltip = displayName.tooltip } as BaseField<Data>; break;
                case int i: elem = new IntegerField { value = i, tooltip = displayName.tooltip } as BaseField<Data>; break;
                case float f: elem = new FloatField { value = f, tooltip = displayName.tooltip } as BaseField<Data>; break;
                default: throw new Exception($"Can't create UI field for type {getter().GetType()}, please add it if it's relevant. If you can't consider using TargetPropertyGUIContext.AddProperty instead.");
            }

            if (elem != null)
            {
                context.AddProperty<Data>(displayName.text, indentLevel, elem, (evt) => {
                    if (Equals(getter(), evt.newValue))
                        return;

                    registerUndo(displayName.text);
                    setter(evt.newValue);
                    onChange();
                });
            }
            else
            {
                context.AddProperty<Enum>(displayName.text, indentLevel, elemEnum, (evt) => {
                    if (Equals(getter(), evt.newValue))
                        return;

                    registerUndo(displayName.text);
                    setter((Data)(object)evt.newValue);
                    onChange();
                });
            }
        }

        protected void AddFoldout(string text, Func<bool> getter, Action<bool> setter)
            => AddFoldout(new GUIContent(text), getter, setter);

        protected void AddFoldout(GUIContent content, Func<bool> getter, Action<bool> setter)
        {
            var foldout = new Foldout()
            {
                value = getter(),
                text = content.text,
                tooltip = content.tooltip
            };

            foldout.RegisterValueChangedCallback((evt) => {
                setter(evt.newValue);
                onChange();
            });

            // Apply padding:
            foldout.style.paddingLeft = context.globalIndentLevel * 15;

            context.Add(foldout);
        }

        public void CreatePropertyGUIWithHeader()
        {
            if (!String.IsNullOrEmpty(title))
            {
                int index = foldoutIndex;
               
                context.globalIndentLevel++;
                CreatePropertyGUI();
                context.globalIndentLevel--;
            }
            else
                CreatePropertyGUI();
        }

        protected abstract void CreatePropertyGUI();

        /// <summary>Warning: this property must have a different value for each property block type!</summary>
        protected abstract int foldoutIndex { get; }
    }

    static class CoreIncludes
    {
        const string kColor = "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl";
        const string kTexture = "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl";
        const string kTextureStack = "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl";
        const string kSGFunc = "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl";

        public static readonly IncludeCollection CorePregraph = new IncludeCollection
        {
            { kColor, IncludeLocation.Pregraph },
            { kTexture, IncludeLocation.Pregraph },
            { kTextureStack, IncludeLocation.Pregraph },
            { kSGFunc, IncludeLocation.Pregraph },        // TODO: put this on a conditional
        };
    }
}
