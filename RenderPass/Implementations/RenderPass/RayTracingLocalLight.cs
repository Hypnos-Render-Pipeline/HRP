using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class RayTracingLocalLight : BaseRenderPass
    {
        [NodePin(PinType.In, true)]
        public LightListPin lights = new LightListPin();

        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        [NodePin(PinType.In, true)]
        public TexturePin ao = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.DefaultHDR, 0));

        static MaterialWithName lightingMat = new MaterialWithName("Hidden/DeferredLighting");

        static RayTracingShader rtShader = Resources.Load<RayTracingShader>("Shaders/Tools/RTShadow");

        static BNSLoader bnsLoader = BNSLoader.instance;

        ComputeBuffer lightConstBuffer;
        LightStructGPU[] lightStructGPU = new LightStructGPU[1];
        int lsGPUsize;

        public RayTracingLocalLight()
        {
            lsGPUsize = Marshal.SizeOf<LightStructGPU>();
            lightConstBuffer = new ComputeBuffer(1, lsGPUsize, ComputeBufferType.Constant);
        }

        public override void Dispose()
        {
            lightConstBuffer.Dispose();
        }

        public override void Excute(RenderContext context)
        {
            var cam = context.camera;
            var cpos = cam.transform.position;
            var cb = context.commandBuffer;

            cb.SetGlobalTexture("_DepthTex", depth.handle);
            cb.SetGlobalTexture("_AOTex", ao);

            int shadowRT = Shader.PropertyToID("ShadowRT");
            var desc = target.desc.basicDesc;
            desc.enableRandomWrite = true;
            desc.colorFormat = RenderTextureFormat.R8;
            desc.depthBufferBits = 0;
            cb.GetTemporaryRT(shadowRT, desc);

            var acc = RTRegister.AccStruct();
            cb.SetRayTracingAccelerationStructure(rtShader, "_RaytracingAccelerationStructure", acc);

            cb.SetGlobalTexture("_Sobol", bnsLoader.tex_sobol);
            cb.SetGlobalTexture("_ScramblingTile", bnsLoader.tex_scrambling);
            cb.SetGlobalTexture("_RankingTile", bnsLoader.tex_rankingTile);
            cb.SetGlobalTexture("_RayTracedLocalShadowMask", shadowRT);

            cb.SetGlobalVector("_Pixel_WH", new Vector4(desc.width, desc.height, 1.0f / desc.width, 1.0f / desc.height));

            cb.SetRayTracingShaderPass(rtShader, "RTGI");
            cb.SetGlobalConstantBuffer(lightConstBuffer, "_TargetLocalLight", 0, lsGPUsize);

            cb.SetRenderTarget(target);

            foreach (var light in lights.handle.locals)
            {
                if (light.shadow == HRPLightShadowType.RayTrace)
                {
                    lightStructGPU[0] = light.lightStructGPU;
                    cb.SetComputeBufferData(lightConstBuffer, lightStructGPU);
                    cb.SetRayTracingTextureParam(rtShader, "RenderTarget", shadowRT);
                    cb.DispatchRays(rtShader, "LocalLightShadow", (uint)desc.width, (uint)desc.height, 1, cam);

                    Vector3 lpos = light.transform.position;
                    if (Vector3.Distance(cpos, lpos) > light.range * 1.3f)
                        cb.DrawMesh(MeshWithType.sphere, Matrix4x4.TRS(lpos, Quaternion.identity, Vector3.one * (light.range * 2)), lightingMat, 0, 6);
                    else
                        cb.Blit(null, target, lightingMat, 5);
                }
            }

            cb.ReleaseTemporaryRT(shadowRT);
        }
    }
}