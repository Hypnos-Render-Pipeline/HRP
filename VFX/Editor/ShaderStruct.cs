using Data.Util;
using HypnosRenderPipeline.Tools;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

namespace HypnosRenderPipeline.GraphWrapper.Editor
{
    struct LitPreZ
    {
        public struct VertexDescriptionInputs
        {
            Vector3 TimeParameters;
            Vector3 ObjectSpacePosition;
            Vector3 WorldSpacePosition;
            Vector3 ObjectSpaceNormal;
            Vector3 WorldSpaceNormal;
            Vector3 ObjectSpaceTangent;
            Vector4 uv0;
        };

        public struct SurfaceDescriptionInputs
        { };

        static ShaderPass shaderPass = new ShaderPass()
        {
            displayName = "PreZ",
            referenceName = "PreZ",
            lightMode = "PreZ",
            ColorMaskOverride = "ColorMask 0",
            useInPreview = true,
            vertexPorts = new List<int>(){
                 LitMasterNode.PositionSlotId,
                },
            pixelPorts = new List<int>() { },
            includes = new List<string>() { },
            passInclude = PathDefine.path + "VFX/Resources/Shaders/Lit.hlsl",
            varyingsInclude = PathDefine.path + "VFX/Resources/Shaders/Lit.hlsl",
            pragmas = new List<string>() { },
            keywords = new KeywordDescriptor[] { },
        };

        static public void GenerateShaderPass(LitMasterNode node, GenerationMode mode, ShaderGenerator result, List<string> sourceAssetDependencyPaths)
        {
            var activeFields = GetActiveFieldsFromMasterNode(node, shaderPass);
            GenerationUtils.GenerateShaderPass(node, shaderPass, mode, activeFields, result, sourceAssetDependencyPaths, new List<Dependency[]>(), typeof(LitPreZ).FullName, Assembly.GetExecutingAssembly().FullName);
        }

        static ActiveFields GetActiveFieldsFromMasterNode(LitMasterNode masterNode, ShaderPass pass)
        {
            var activeFields = new ActiveFields();
            var baseActiveFields = activeFields.baseInstance;

            // Graph Vertex
            if (masterNode.IsSlotConnected(LitMasterNode.PositionSlotId))
                baseActiveFields.Add("features.graphVertex");

            // Graph Pixel (always enabled)
            baseActiveFields.Add("features.graphPixel");

            //// NormalMap
            //if (masterNode.IsSlotConnected(PBRMasterNode.NormalSlotId))
            //    baseActiveFields.Add("Normal");

            return activeFields;
        }
    }


    struct GBuffer_Equal
    {
        public struct VertexDescriptionInputs
        {
            Vector3 TimeParameters;
            Vector3 ObjectSpacePosition;
            Vector3 WorldSpacePosition;
            Vector3 ObjectSpaceNormal;
            Vector3 WorldSpaceNormal;
            Vector3 ObjectSpaceTangent;
            Vector4 uv0;
        };
        public struct SurfaceDescriptionInputs
        {
            Vector3 TimeParameters;
            Vector3 TangentSpaceNormal;
            Vector3 WorldSpacePosition;
            Vector3 WorldSpaceNormal;
            Vector4 uv0;
        };

        static ShaderPass shaderPass = new ShaderPass()
        {
            displayName = "GBuffer_Equal",
            referenceName = "GBuffer_Equal",
            lightMode = "GBuffer_Equal",
            ZWriteOverride = "ZWrite off",
            ZTestOverride = "ZTest Equal",
            useInPreview = true,
            vertexPorts = new List<int>(){
                 LitMasterNode.PositionSlotId,
                 LitMasterNode.VertexNormalSlotId,
                 LitMasterNode.VertexUVSlotId,
                },
            pixelPorts = new List<int>() {
                LitMasterNode.AlbedoSlotId,
                LitMasterNode.NormalSlotId,
                LitMasterNode.MetallicSlotId,
                LitMasterNode.SmoothnessSlotId,
                LitMasterNode.EmissionSlotId,
                LitMasterNode.OcclusionSlotId,
            },
            includes = new List<string>() { PathDefine.path + "Lighting/Runtime/Resources/Shaders/Includes/GBuffer.hlsl" },
            passInclude = PathDefine.path + "VFX/Resources/Shaders/Lit.hlsl",
            varyingsInclude = PathDefine.path + "VFX/Resources/Shaders/Lit.hlsl",
            pragmas = new List<string>() { },
            keywords = new KeywordDescriptor[] { },
            requiredAttributes = new List<string> { "Aldedo", "Metallic", "Smoothness", "Emission", "Occlusion" }
        };

        static public void GenerateShaderPass(LitMasterNode node, GenerationMode mode, ShaderGenerator result, List<string> sourceAssetDependencyPaths)
        {
            var activeFields = GetActiveFieldsFromMasterNode(node, shaderPass);
            GenerationUtils.GenerateShaderPass(node, shaderPass, mode, activeFields, result, sourceAssetDependencyPaths, new List<Dependency[]>(), typeof(GBuffer_Equal).FullName, Assembly.GetExecutingAssembly().FullName);
        }

        static ActiveFields GetActiveFieldsFromMasterNode(LitMasterNode masterNode, ShaderPass pass)
        {
            var activeFields = new ActiveFields();
            var baseActiveFields = activeFields.baseInstance;

            // Graph Vertex
            if (masterNode.IsSlotConnected(LitMasterNode.PositionSlotId) || masterNode.IsSlotConnected(LitMasterNode.VertexNormalSlotId))
                baseActiveFields.Add("features.graphVertex");

            // Graph Pixel (always enabled)
            baseActiveFields.Add("features.graphPixel");

            //// NormalMap
            //if (masterNode.IsSlotConnected(PBRMasterNode.NormalSlotId))
            //    baseActiveFields.Add("Normal");

            return activeFields;
        }
    }
}