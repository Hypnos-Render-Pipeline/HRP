using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline
{
    public class FrustumCulling
    {
        static ComputeShaderWithName cullingShader = ComputeShaderWithName.cullingShader;

        static Vector4[] planes = new Vector4[6];

        public static void SetCullingCamera(CommandBuffer cb, Camera cam)
        {
            var planes_ = GeometryUtility.CalculateFrustumPlanes(cam);
            for (int i = 0; i < 6; i++)
            {
                planes[i] = planes_[i].normal;
                planes[i].w = -planes_[i].distance;
            }

            cb.SetGlobalVectorArray("_FrustumPlanes", planes);
        }

        public static void CullPlanes(CommandBuffer cb, ComputeBuffer argsBuffer, ComputeBuffer offsetBuffer, ComputeBuffer indexBuffer)
        {
            cb.SetComputeBufferParam(cullingShader, 0, "_ResultNum", argsBuffer);
            cb.DispatchCompute(cullingShader, 0, 1, 1, 1);

            cb.SetComputeIntParam(cullingShader, "_TotalNum", offsetBuffer.count);
            cb.SetComputeBufferParam(cullingShader, 1, "_ResultNum", argsBuffer);
            cb.SetComputeBufferParam(cullingShader, 1, "_Results", indexBuffer);
            cb.SetComputeBufferParam(cullingShader, 1, "_PlaneCenter", offsetBuffer);
            cb.DispatchCompute(cullingShader, 1, offsetBuffer.count / 32 + (offsetBuffer.count % 32 != 0 ? 1 : 0), 1, 1);
        }
    }
}