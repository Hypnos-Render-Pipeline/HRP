using System;
using UnityEditor;
using UnityEditor.ShaderGraph;

namespace HypnosRenderPipeline.VFX.ShaderGraph
{
    static class CreateLitShaderGraph
    {
        [MenuItem("Assets/Create/Shader/HRP/Lit Shader Graph", false, 208)]
        public static void CreateHDLitGraph()
        {
            var target = (HRPTarget)Activator.CreateInstance(typeof(HRPTarget));
            target.TrySetActiveSubTarget(typeof(HRPLitSubTarget));

            var blockDescriptors = new[]
            {
                BlockFields.VertexDescription.Position,
                BlockFields.VertexDescription.Normal,
                BlockFields.VertexDescription.Tangent,
            };

            GraphUtil.CreateNewGraphWithOutputs(new[] { target }, blockDescriptors);
        }
    }
}