using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ShaderGraph.Legacy;
using UnityEngine;
using HypnosRenderPipeline.Tools;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using System;
using System.Runtime.InteropServices.WindowsRuntime;

namespace HypnosRenderPipeline.VFX.ShaderGraph
{

    sealed partial class HRPFogSubTarget : HRPSubTarget, ILegacyTarget
    {
        public HRPFogSubTarget() => displayName = "Fog";

        static readonly GUID kSubTargetSourceCodeGuid = new GUID("944a69d407e08384698752c91d56ec3a");

        protected override GUID subTargetAssetGuid => kSubTargetSourceCodeGuid;
        protected override ShaderID shaderID => ShaderID.Lit;

        protected override FieldDescriptor subShaderField => new FieldDescriptor("SubShader", "Fog Subshader", "");
        protected override string customInspector => "";

        protected override string renderType => "Fog";
        protected override string renderQueue => "Transparent+1";

        public override void GetFields(ref TargetFieldContext context)
        {
            base.GetFields(ref context);
        }

        // Trick for non-intrusive add custom field to SG.
        [GenerateBlocks]
        public struct SurfaceDescription
        // Do not modify the above lines!!
        {
            public static string name = "SurfaceDescription";
            public static BlockFieldDescriptor density = new BlockFieldDescriptor(SurfaceDescription.name, "Density", "Density", "SURFACEDESCRIPTION_DENSITY",
                new FloatControl(1), ShaderStage.Fragment);
            public static BlockFieldDescriptor scatter_rate = new BlockFieldDescriptor(SurfaceDescription.name, "ScatterRate", "Scatter Rate", "SURFACEDESCRIPTION_SCATTERRATE",
                new FloatControl(0.5f), ShaderStage.Fragment);
            public static BlockFieldDescriptor g = new BlockFieldDescriptor(SurfaceDescription.name, "PhaseG", "PhaseG", "SURFACEDESCRIPTION_PHASEG",
                new FloatControl(0), ShaderStage.Fragment);
        }

        public override void GetActiveBlocks(ref TargetActiveBlockContext context)
        {
            context.AddBlock(SurfaceDescription.density);
            context.AddBlock(SurfaceDescription.scatter_rate);
            context.AddBlock(SurfaceDescription.g);
        }

        public override void CollectShaderProperties(PropertyCollector collector, GenerationMode generationMode)
        {
            // Note: Due to the shader graph framework it is not possible to rely on litData.emissionOverriden
            // to decide to add the ForceForwardEmissive property or not. The emissionOverriden setup is done after
            // the call to AddShaderProperty
            collector.AddShaderProperty(new BooleanShaderProperty
            {
                value = false,
                hidden = true,
                overrideHLSLDeclaration = true,
                hlslDeclarationOverride = HLSLDeclaration.DoNotDeclare,
                overrideReferenceName = "CCC",
            });
        }

        protected override void CollectPassKeywords(ref PassDescriptor pass)
        {
            base.CollectPassKeywords(ref pass);
            //pass.keywords.Add(RefractionKeyword);
        }
        protected override IEnumerable<SubShaderDescriptor> EnumerateSubShaders()
        {
            yield return PostProcessSubShader(GetRasterSubShaderDescriptor());
        }

        public bool TryUpgradeFromMasterNode(IMasterNode1 masterNode, out Dictionary<BlockFieldDescriptor, int> blockMap)
        {
            throw new System.NotImplementedException();
        }

        IncludeCollection RasterIncludes()
        {
            var includes = new IncludeCollection();
            includes.Add($"{templateMaterialDirectories[1]}FogInclude.hlsl", IncludeLocation.Pregraph);
            return includes;
        }

        PassDescriptor WriteFog() {
            return new PassDescriptor() {
                // Definition
                displayName = "Fog",
                referenceName = "Fog",
                lightMode = "Fog",
                useInPreview = true,
                validVertexBlocks = new BlockFieldDescriptor[] { },

                validPixelBlocks = new BlockFieldDescriptor[] { SurfaceDescription.density, SurfaceDescription.scatter_rate, SurfaceDescription.g },
                passTemplatePath = $"{templateMaterialDirectories[0]}FogVolume.template",

                // Collections
                renderStates = new RenderStateCollection() { RenderState.ColorMask("ColorMask 0"), RenderState.Cull(Cull.Front), RenderState.ZWrite(ZWrite.Off), RenderState.ZTest(ZTest.Always) },
                includes = RasterIncludes(),
                structs = new StructCollection() { }
            };
        }

        SubShaderDescriptor GetRasterSubShaderDescriptor()
        {
            return new SubShaderDescriptor
            {
                generatesPreview = true,
                passes = GetPasses(),
                customTags = "\"Name\" = \"Rasterization\""
            };

            PassCollection GetPasses()
            {
                var passes = new PassCollection();

                passes.Add(WriteFog());
                return passes;
            }
        }

        protected override void AddInspectorPropertyBlocks(SubTargetPropertiesGUI blockList)
        {
            //blockList.AddPropertyBlock();
        }
    }
}