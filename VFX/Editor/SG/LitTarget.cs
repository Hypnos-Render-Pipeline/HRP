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

    sealed partial class HRPLitSubTarget : HRPSubTarget, ILegacyTarget
    {
        public HRPLitSubTarget() => displayName = "Lit";

        static readonly GUID kSubTargetSourceCodeGuid = new GUID("7fb36b8a4231e704c9c09f5ada28cabf");

        protected override GUID subTargetAssetGuid => kSubTargetSourceCodeGuid;
        protected override ShaderID shaderID => ShaderID.Lit;

        protected override FieldDescriptor subShaderField => new FieldDescriptor("SubShader", "Lit Subshader", "");
        protected override string customInspector => "";

        protected override string renderType => "Lit";
        protected override string renderQueue => "Common";

        // Refraction
        public static FieldDescriptor Refraction = new FieldDescriptor(string.Empty, "Refraction", "");
        public static KeywordDescriptor RefractionKeyword = new KeywordDescriptor()
        {
            displayName = "Refraction Model",
            referenceName = "_REFRACTION",
            type = KeywordType.Enum,
            definition = KeywordDefinition.ShaderFeature,
            scope = KeywordScope.Local,
            entries = new KeywordEntry[]
            {
                new KeywordEntry() { displayName = "Off", referenceName = "OFF" },
                new KeywordEntry() { displayName = "Plane", referenceName = "PLANE" },
                new KeywordEntry() { displayName = "Sphere", referenceName = "SPHERE" },
                new KeywordEntry() { displayName = "Thin", referenceName = "THIN" },
            }
        };

        public override void GetFields(ref TargetFieldContext context)
        {
            base.GetFields(ref context);

            if (context.connectedBlocks.Contains(BlockFields.VertexDescription.Position))
                context.AddField(new FieldDescriptor("Connected", "Position", ""));
        }

        public override void GetActiveBlocks(ref TargetActiveBlockContext context)
        {
            context.AddBlock(BlockFields.VertexDescription.Position);
            context.AddBlock(BlockFields.VertexDescription.Normal);
            context.AddBlock(BlockFields.VertexDescription.Tangent);

            context.AddBlock(BlockFields.SurfaceDescription.BaseColor);
            context.AddBlock(BlockFields.SurfaceDescription.NormalTS);
            context.AddBlock(BlockFields.SurfaceDescription.Metallic);
            context.AddBlock(BlockFields.SurfaceDescription.Emission);
            context.AddBlock(BlockFields.SurfaceDescription.Smoothness);
            context.AddBlock(BlockFields.SurfaceDescription.Occlusion);
            context.AddBlock(BlockFields.SurfaceDescription.Alpha);
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
            yield return PostProcessSubShader(GetRaySubShaderDescriptor());
        }

        public bool TryUpgradeFromMasterNode(IMasterNode1 masterNode, out Dictionary<BlockFieldDescriptor, int> blockMap)
        {
            throw new System.NotImplementedException();
        }

        IncludeCollection RasterIncludes()
        {
            var includes = new IncludeCollection();
            includes.Add($"{templateMaterialDirectories[1]}LitInclude.hlsl", IncludeLocation.Pregraph);
            return includes;
        }

        IncludeCollection RayIncludes()
        {
            var includes = new IncludeCollection();
            includes.Add($"{templateMaterialDirectories[1]}RT/Include/RtLitInclude.hlsl", IncludeLocation.Pregraph);
            return includes;
        }

        PassDescriptor PreZ() {
            return new PassDescriptor() {
                // Definition
                displayName = "PreZ",
                referenceName = "PreZ",
                lightMode = "PreZ",
                useInPreview = true,
                validVertexBlocks = new BlockFieldDescriptor[]
                {
                    BlockFields.VertexDescription.Position
                },

                validPixelBlocks = new BlockFieldDescriptor[] { },
                passTemplatePath = $"{templateMaterialDirectories[0]}PreZ.template",

                // Collections
                renderStates = new RenderStateCollection() { RenderState.ColorMask("ColorMask 0") },
                includes = RasterIncludes(),
                structs = new StructCollection() { }
            };
        }

        PassDescriptor GBuffer_LEqual() {
            return new PassDescriptor() {
                // Definition
                displayName = "GBuffer_LEqual",
                referenceName = "GBuffer_LEqual",
                lightMode = "GBuffer_LEqual",
                useInPreview = true,
                validVertexBlocks = new BlockFieldDescriptor[]
                {
                    BlockFields.VertexDescription.Position,
                    BlockFields.VertexDescription.Normal,
                    BlockFields.VertexDescription.Tangent,
                },

                validPixelBlocks = new BlockFieldDescriptor[] {
                    BlockFields.SurfaceDescription.BaseColor,
                    BlockFields.SurfaceDescription.NormalTS,
                    BlockFields.SurfaceDescription.Metallic,
                    BlockFields.SurfaceDescription.Emission,
                    BlockFields.SurfaceDescription.Smoothness,
                    BlockFields.SurfaceDescription.Occlusion,
                    BlockFields.SurfaceDescription.Alpha,
                },
                passTemplatePath = $"{templateMaterialDirectories[0]}GBuffer_LEqual.template",

                includes = RasterIncludes(),
                structs = new StructCollection() { }
            };
        }

        PassDescriptor GBuffer_Equal()
        {
            var res = GBuffer_LEqual();
            res.renderStates = new RenderStateCollection() { RenderState.ZWrite("off"), RenderState.ZTest("Equal") };
            res.displayName = res.referenceName = res.lightMode = "GBuffer_Equal";
            return res;
        }

        PassDescriptor VRender()
        {
            return new PassDescriptor()
            {
                // Definition
                displayName = "RT",
                referenceName = "RT",
                lightMode = "RT",
                useInPreview = false,
                validVertexBlocks = new BlockFieldDescriptor[] { },

                validPixelBlocks = new BlockFieldDescriptor[] {
                    BlockFields.SurfaceDescription.BaseColor,
                    BlockFields.SurfaceDescription.NormalTS,
                    BlockFields.SurfaceDescription.Metallic,
                    BlockFields.SurfaceDescription.Emission,
                    BlockFields.SurfaceDescription.Smoothness,
                    BlockFields.SurfaceDescription.Occlusion,
                    BlockFields.SurfaceDescription.Alpha,
                },
                passTemplatePath = $"{templateMaterialDirectories[0]}VRender.template",

                includes = RayIncludes(),
                structs = new StructCollection() { }
            };
        }

        PassDescriptor RTGI()
        {
            return new PassDescriptor()
            {
                // Definition
                displayName = "RTGI",
                referenceName = "RTGI",
                lightMode = "RTGI",
                useInPreview = false,
                validVertexBlocks = new BlockFieldDescriptor[] { },

                validPixelBlocks = new BlockFieldDescriptor[] {
                    BlockFields.SurfaceDescription.BaseColor,
                    BlockFields.SurfaceDescription.NormalTS,
                    BlockFields.SurfaceDescription.Metallic,
                    BlockFields.SurfaceDescription.Emission,
                    BlockFields.SurfaceDescription.Smoothness,
                    BlockFields.SurfaceDescription.Occlusion,
                    BlockFields.SurfaceDescription.Alpha,
                },
                passTemplatePath = $"{templateMaterialDirectories[0]}RTGI.template",

                includes = RayIncludes(),
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

                passes.Add(PreZ());
                passes.Add(GBuffer_Equal());
                passes.Add(GBuffer_LEqual());
                //passes.Add(PreZ());
                return passes;
            }
        }

        SubShaderDescriptor GetRaySubShaderDescriptor()
        {
            return new SubShaderDescriptor
            {
                generatesPreview = false,
                passes = GetPasses(),
                customTags = "\"Name\" = \"RayTrace\""
            };

            PassCollection GetPasses()
            {
                var passes = new PassCollection();
                passes.Add(VRender());
                passes.Add(RTGI());
                return passes;
            }
        }

        //public override void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange, Action<string> registerUndo)
        //{
        //    throw new NotImplementedException();
        //}

        protected override void AddInspectorPropertyBlocks(SubTargetPropertiesGUI blockList)
        {
            //blockList.AddPropertyBlock();
        }

        //protected override int ComputeMaterialNeedsUpdateHash()
        //{
        //    int hash = base.ComputeMaterialNeedsUpdateHash();

        //    unchecked
        //    {
        //        bool subsurfaceScattering = litData.materialType == HDLitData.MaterialType.SubsurfaceScattering;
        //        hash = hash * 23 + subsurfaceScattering.GetHashCode();
        //        hash = hash * 23 + litData.emissionOverriden.GetHashCode();
        //    }

        //    return hash;
        //}
    }
}