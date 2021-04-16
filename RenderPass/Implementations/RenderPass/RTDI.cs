using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline.RenderPass
{
    public class RTDI : BaseRenderPass
    {
        [NodePin(PinType.In, true)]
        public LightListPin lights = new LightListPin();

        [NodePin(PinType.In, true)]
        public BufferPin<LightStructGPU> lightBuffer = new BufferPin<LightStructGPU>(1);

        [NodePin(PinType.In, true)]
        public BufferPin<uint> tiledLights = new BufferPin<uint>(1);

        [NodePin(PinType.InOut)]
        public BufferPin<LightStructGPU> directionalLightBuffer = new BufferPin<LightStructGPU>(1);

        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        [NodePin(PinType.In, true)]
        public TexturePin diffuse = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin specular = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin normal = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin emission = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In)]
        public TexturePin ao = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.Out)]
        public TexturePin lightingResult = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.DefaultHDR, 0),
                                                                SizeCastMode.Fixed,
                                                                ColorCastMode.Fixed,
                                                                SizeScale.Full);

        public bool includeSunlight = false;

        static MaterialWithName lightingMat = new MaterialWithName("Hidden/DeferredLighting");

        static RayTracingShader rtShader = Resources.Load<RayTracingShader>("Shaders/Tools/RTDI");

        static BNSLoader bnsLoader = BNSLoader.instance;

        List<LightStructGPU> lightBufferCPU = new List<LightStructGPU>();

        public override void Execute(RenderContext context)
        {
            var cam = context.camera;
            var cpos = cam.transform.position;
            var cb = context.commandBuffer;

            int target = Shader.PropertyToID("RTDI_RT");
            var desc = lightingResult.desc.basicDesc;
            desc.enableRandomWrite = true;
            desc.depthBufferBits = 0;
            cb.GetTemporaryRT(target, desc);

            var acc = RTRegister.AccStruct();
            acc.Build();
            cb.SetRayTracingAccelerationStructure(rtShader, "_RaytracingAccelerationStructure", acc);

            cb.SetGlobalTexture("_Sobol", bnsLoader.tex_sobol);
            cb.SetGlobalTexture("_ScramblingTile", bnsLoader.tex_scrambling);
            cb.SetGlobalTexture("_RankingTile", bnsLoader.tex_rankingTile);

            cb.SetGlobalVector("_Pixel_WH", new Vector4(desc.width, desc.height, 1.0f / desc.width, 1.0f / desc.height));

            cb.SetRayTracingShaderPass(rtShader, "RTGI");

            cb.SetRayTracingTextureParam(rtShader, "RenderTarget", target);
            cb.DispatchRays(rtShader, "LocalLight", (uint)desc.width, (uint)desc.height, 1, cam);

            if (!directionalLightBuffer.connected)
            {
                lightBufferCPU.Clear();
                foreach (var light in lights.handle.directionals)
                {
                    if (includeSunlight || !light.sunLight)
                        lightBufferCPU.Add(light.lightStructGPU);
                }
                directionalLightBuffer.ReSize(lightBufferCPU.Count);
                cb.SetGlobalInt("_DirecionalLightCount", lightBufferCPU.Count);
                cb.SetBufferData(directionalLightBuffer, lightBufferCPU);
                cb.SetGlobalBuffer("_DirecionalLightBuffer", directionalLightBuffer);
            }

            cb.DispatchRays(rtShader, "DirecionalLight", (uint)desc.width, (uint)desc.height, 1, cam);

            cb.Blit(target, lightingResult);
            cb.ReleaseTemporaryRT(target);
        }
    }
}