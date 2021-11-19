using System;
using UnityEditor;
using UnityEditor.ShaderGraph;

namespace HypnosRenderPipeline.VFX.ShaderGraph
{
    static class CreateFogShaderGraph
    {
        [MenuItem("Assets/Create/Shader/HRP/FogVolume Shader Graph", false, 209)]
        public static void CreateHDLitGraph()
        {
            var target = (HRPTarget)Activator.CreateInstance(typeof(HRPTarget));
            target.TrySetActiveSubTarget(typeof(HRPFogSubTarget));

            var blockDescriptors = new[]
            {
                HRPFogSubTarget.SurfaceDescription.density,
                HRPFogSubTarget.SurfaceDescription.scatter_rate,
                HRPFogSubTarget.SurfaceDescription.g
            };

            GraphUtil.CreateNewGraphWithOutputs(new[] { target }, blockDescriptors);
        }
    }
}