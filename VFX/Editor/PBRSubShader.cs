using System.Collections.Generic;
using UnityEditor.ShaderGraph;
using UnityEngine.Rendering;
using HypnosRenderPipeline.RenderGraph;
using UnityEngine;
using UnityEditor.ShaderGraph.Internal;
using UnityEditor;
using System.Linq;

namespace HypnosRenderPipeline.GraphWrapper.Editor
{
    [Title(new[] { "Master", "Lit" })]
    internal class LitMasterNode : MaterialMasterNode<ILitSubShader>, IMayRequirePosition, IMayRequireNormal, IMayRequireTangent
    {

        public const string AlbedoSlotName = "Albedo";
        public const int VertexUVSlotId = 10;
        public const int VertexNormalSlotId = 9;
        public const int PositionSlotId = 8;
        public const int AlphaThresholdSlotId = 7;
        public const int AlphaSlotId = 6;
        public const int OcclusionSlotId = 5;
        public const int SmoothnessSlotId = 4;
        public const int EmissionSlotId = 3;
        public const int MetallicSlotId = 2;
        public const int NormalSlotId = 1;
        public const int AlbedoSlotId = 0;
        public const string UVName = "Vertex UV";
        public const string NormalName = "Vertex Normal";
        public const string PositionName = "Vertex Position";
        public const string AlphaClipThresholdSlotName = "AlphaClipThreshold";
        public const string AlphaSlotName = "Alpha";
        public const string OcclusionSlotName = "Occlusion";
        public const string SmoothnessSlotName = "Smoothness";
        public const string MetallicSlotName = "Metallic";
        public const string EmissionSlotName = "Emission";
        public const string NormalSlotName = "Normal";

        public bool twoSided { get; set; }



        public sealed override void UpdateNodeAfterDeserialization()
        {
            base.UpdateNodeAfterDeserialization();
            name = "Lit Master";
            AddSlot(new PositionMaterialSlot(PositionSlotId, PositionName, PositionName, CoordinateSpace.Object, ShaderStageCapability.Vertex));
            AddSlot(new NormalMaterialSlot(VertexNormalSlotId, NormalName, NormalName, CoordinateSpace.Object, ShaderStageCapability.Vertex));
            AddSlot(new UVMaterialSlot(VertexUVSlotId, UVName, UVName, UVChannel.UV0, ShaderStageCapability.Vertex));
            AddSlot(new ColorRGBMaterialSlot(AlbedoSlotId, AlbedoSlotName, AlbedoSlotName, UnityEditor.Graphing.SlotType.Input, Color.white, ColorMode.Default, ShaderStageCapability.Fragment));
            var coordSpace = CoordinateSpace.Tangent;

            AddSlot(new NormalMaterialSlot(NormalSlotId, NormalSlotName, NormalSlotName, coordSpace, ShaderStageCapability.Fragment));
            AddSlot(new ColorRGBMaterialSlot(EmissionSlotId, EmissionSlotName, EmissionSlotName, UnityEditor.Graphing.SlotType.Input, Color.black, ColorMode.Default, ShaderStageCapability.Fragment));

            AddSlot(new Vector1MaterialSlot(MetallicSlotId, MetallicSlotName, MetallicSlotName, UnityEditor.Graphing.SlotType.Input, 0, ShaderStageCapability.Fragment));

            AddSlot(new Vector1MaterialSlot(SmoothnessSlotId, SmoothnessSlotName, SmoothnessSlotName, UnityEditor.Graphing.SlotType.Input, 0.5f, ShaderStageCapability.Fragment));
            AddSlot(new Vector1MaterialSlot(OcclusionSlotId, OcclusionSlotName, OcclusionSlotName, UnityEditor.Graphing.SlotType.Input, 1f, ShaderStageCapability.Fragment));
            AddSlot(new Vector1MaterialSlot(AlphaSlotId, AlphaSlotName, AlphaSlotName, UnityEditor.Graphing.SlotType.Input, 1f, ShaderStageCapability.Fragment));
            AddSlot(new Vector1MaterialSlot(AlphaThresholdSlotId, AlphaClipThresholdSlotName, AlphaClipThresholdSlotName, UnityEditor.Graphing.SlotType.Input, 0.0f, ShaderStageCapability.Fragment));

            RemoveSlotsNameNotMatching(new[]
            {
                PositionSlotId,
                VertexNormalSlotId,
                VertexUVSlotId,
                AlbedoSlotId,
                NormalSlotId,
                EmissionSlotId,
                MetallicSlotId,
                SmoothnessSlotId,
                OcclusionSlotId,
                AlphaSlotId,
                AlphaThresholdSlotId
            }, true);
        }



        public NeededCoordinateSpace RequiresNormal(ShaderStageCapability stageCapability = ShaderStageCapability.All)
        {
            List<MaterialSlot> slots = new List<MaterialSlot>();
            GetSlots(slots);

            List<MaterialSlot> validSlots = new List<MaterialSlot>();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].stageCapability != ShaderStageCapability.All && slots[i].stageCapability != stageCapability)
                    continue;

                validSlots.Add(slots[i]);
            }
            return validSlots.OfType<IMayRequireNormal>().Aggregate(NeededCoordinateSpace.None, (mask, node) => mask | node.RequiresNormal(stageCapability));
        }

        public NeededCoordinateSpace RequiresPosition(ShaderStageCapability stageCapability = ShaderStageCapability.All)
        {
            List<MaterialSlot> slots = new List<MaterialSlot>();
            GetSlots(slots);

            List<MaterialSlot> validSlots = new List<MaterialSlot>();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].stageCapability != ShaderStageCapability.All && slots[i].stageCapability != stageCapability)
                    continue;

                validSlots.Add(slots[i]);
            }
            return validSlots.OfType<IMayRequirePosition>().Aggregate(NeededCoordinateSpace.None, (mask, node) => mask | node.RequiresPosition(stageCapability));
        }

        public NeededCoordinateSpace RequiresTangent(ShaderStageCapability stageCapability = ShaderStageCapability.All)
        {
            List<MaterialSlot> slots = new List<MaterialSlot>();
            GetSlots(slots);

            List<MaterialSlot> validSlots = new List<MaterialSlot>();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].stageCapability != ShaderStageCapability.All && slots[i].stageCapability != stageCapability)
                    continue;

                validSlots.Add(slots[i]);
            }
            return validSlots.OfType<IMayRequireTangent>().Aggregate(NeededCoordinateSpace.None, (mask, node) => mask | node.RequiresTangent(stageCapability));
        }

        [MenuItem("HypnosRenderPipeline/Shader/Lit Graph")]
        [MenuItem("Assets/Create/Shader/Lit Graph (HRP)", false, 200)]
        public static void Create()
        {
            GraphUtil.CreateNewGraph(new LitMasterNode());
        }
    }

    interface ILitSubShader : ISubShader { };

    internal class PBRSubShaderRas : ILitSubShader
    {
        public string GetSubshader(IMasterNode masterNode, GenerationMode mode, List<string> sourceAssetDependencyPaths = null)
        {

            var LitNode = masterNode as PBRMasterNode;
            var subShader = new ShaderGenerator();

            subShader.AddShaderChunk("SubShader", true);
            subShader.AddShaderChunk("{", true);
            subShader.Indent();
            {
                subShader.AddShaderChunk("HLSLINCLUDE", true);
                subShader.Indent();
                subShader.AddShaderChunk("#include \"Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl\"");
                //subShader.AddShaderChunk("#include \"Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl\"");
                subShader.AddShaderChunk("#include \"Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariables.hlsl\"");
                //subShader.AddShaderChunk("#include \"Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl\"");
                subShader.AddShaderChunk("bool IsGammaSpace() { return false; }");
                //subShader.AddShaderChunk("float3 SRGBToLinear(float3 c) { return c; }");
                subShader.AddShaderChunk("float3 SRGBToLinear(float3 c) { float3 linearRGBLo = c / 12.92; float3 linearRGBHi = PositivePow((c + 0.055) / 1.055, real3(2.4, 2.4, 2.4)); float3 linearRGB = (c <= 0.04045) ? linearRGBLo : linearRGBHi; return linearRGB; } ");
                //subShader.AddShaderChunk("float3 SRGBToLinear(float3 c) { float3 sRGBLo = c * 12.92; float3 sRGBHi = (PositivePow(c, real3(1.0 / 2.4, 1.0 / 2.4, 1.0 / 2.4)) * 1.055) - 0.055; float3 sRGB = (c <= 0.0031308) ? sRGBLo : sRGBHi; return sRGB; }");
                subShader.AddShaderChunk("float3 UnpackNormal(float4 packednormal) { float3 normal; normal.xy = (packednormal.wy * 2 - 1); normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy))); return normal; }");

                if (mode == GenerationMode.Preview)
                {
                    subShader.AddShaderChunk("#define FVCK_SG_PREVIEW_BUG");
                }

                subShader.Deindent();
                subShader.AddShaderChunk("ENDHLSL", true);

                LitPreZ.GenerateShaderPass(masterNode as LitMasterNode, mode, subShader, sourceAssetDependencyPaths);
                GBuffer_Equal.GenerateShaderPass(masterNode as LitMasterNode, mode, subShader, sourceAssetDependencyPaths);
            }
            subShader.Deindent();
            subShader.AddShaderChunk("}", true);

            return subShader.GetShaderString(0);
        }


        public bool IsPipelineCompatible(RenderPipelineAsset renderPipelineAsset) { return renderPipelineAsset.GetType() == typeof(HypnosRenderPipelineAsset); }
        public int GetPreviewPassIndex() { return 0; }
    }
}
