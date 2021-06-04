using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline.RenderPass
{
    public class RTSunLight : BaseRenderPass
    {
        [NodePin(PinType.In, true)]
        public AfterAtmo afterAtmo = new AfterAtmo();

        [NodePin(PinType.InOut)]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGBHalf));

        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        [NodePin(PinType.In, true)]
        public TexturePin diffuse = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin specular = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin normal = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In)]
        public TexturePin ao = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        static RayTracingShader rtShader = Resources.Load<RayTracingShader>("Shaders/Tools/RTDI");

        static BNSLoader bnsLoader = BNSLoader.instance;

        public override void Execute(RenderContext context)
        {
            if (!afterAtmo.connected || afterAtmo.atmo == null) return;

            var cam = context.camera;
            var cb = context.commandBuffer;

            int temp_target = Shader.PropertyToID("RTDI_RT");
            var desc = target.desc.basicDesc;
            desc.enableRandomWrite = true;
            desc.depthBufferBits = 0;
            cb.GetTemporaryRT(temp_target, desc);

            if (target.connected)
                cb.CopyTexture(target, temp_target);
            else
            {
                cb.SetRenderTarget(target);
                cb.ClearRenderTarget(false, true, Color.clear);
            }

            var acc = RTRegister.AccStruct();
            acc.Build();
            cb.SetRayTracingAccelerationStructure(rtShader, "_RaytracingAccelerationStructure", acc);

            cb.SetGlobalTexture("_Sobol", bnsLoader.tex_sobol);
            cb.SetGlobalTexture("_ScramblingTile", bnsLoader.tex_scrambling);
            cb.SetGlobalTexture("_RankingTile", bnsLoader.tex_rankingTile);

            cb.SetGlobalVector("_Pixel_WH", new Vector4(desc.width, desc.height, 1.0f / desc.width, 1.0f / desc.height));

            cb.SetRayTracingShaderPass(rtShader, "RTGI");

            cb.SetRayTracingTextureParam(rtShader, "RenderTarget", temp_target);
            cb.DispatchRays(rtShader, "SunLight", (uint)desc.width, (uint)desc.height, 1, cam);

            cb.CopyTexture(temp_target, target);
            cb.ReleaseTemporaryRT(temp_target);
        }
    }
}