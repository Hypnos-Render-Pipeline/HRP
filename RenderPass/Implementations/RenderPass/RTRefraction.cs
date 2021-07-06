using HypnosRenderPipeline.Tools;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class RTRefraction : BaseRenderPass
    {

        [NodePin(PinType.In, true)]
        public BufferPin<LightStructGPU> lightBuffer = new BufferPin<LightStructGPU>(1);

        [NodePin(PinType.In, true)]
        public BufferPin<uint> tiledLights = new BufferPin<uint>(1);

        [NodePin(PinType.In)]
        public LightListPin lights = new LightListPin();

        [NodePin(PinType.In)]
        public BufferPin<SunAtmo.SunLight> sun = new BufferPin<SunAtmo.SunLight>(1);

        [NodePin(PinType.InOut, true)]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGBHalf));

        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        [NodePin(PinType.In)]
        public TexturePin motion = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.RGFloat, 0), colorCastMode: ColorCastMode.Fixed);

        //[NodePin(PinType.In, true)]
        //public TexturePin diffuse = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin specular = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin normal = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In)]
        public TexturePin skybox = new TexturePin(new RenderTextureDescriptor(2, 2, RenderTextureFormat.Default) { dimension = TextureDimension.Cube }, sizeScale: SizeScale.Custom);

        [Range(2, 16)]
        public int maxDepth = 4;

        public bool useRTShadow = false;

        ComputeBuffer blockBuffer;
        ComputeBuffer blockBuffer_;
        ComputeBuffer argsBuffer;
        ComputeBuffer argsBuffer_;
        int[] clearArray = new int[3] { 0, 1, 1 };

        static RTShaderWithName rtShader = new RTShaderWithName("Shaders/RTGI/RTRefraction");

        static BNSLoader bnsLoader = BNSLoader.instance;

        List<LightStructGPU> lightBufferCPU;

        struct CSPass
        {
            public static int DownSampleDepth = 0;
            public static int TTFilter = 1;
            public static int SFilter = 2;
            public static int SFilterIndirect = 3;
            public static int UpSample = 4;
            public static int FinalSynthesis = 5;
        }

        public override void Execute(RenderContext context)
        {
            var cb = context.commandBuffer;

            cb.SetGlobalTexture("_SceneColor", target);
            cb.SetGlobalTexture("_HiZDepthTex", depth);

            cb.SetGlobalTexture("_SkyBox", skybox);

            int2 wh = new int2(target.desc.basicDesc.width, target.desc.basicDesc.height);

            cb.SetGlobalVector("_WH", new Vector4(wh.x, wh.y, 1.0f / wh.x, 1.0f / wh.y));
            cb.SetGlobalInt("_UseRTShadow", useRTShadow ? 1 : 0);
            cb.SetGlobalInt("_MaxDepth", maxDepth);
            

            cb.SetGlobalTexture("_Sobol", bnsLoader.tex_sobol);
            cb.SetGlobalTexture("_ScramblingTile", bnsLoader.tex_scrambling);
            cb.SetGlobalTexture("_RankingTile", bnsLoader.tex_rankingTile);

            int tempRef = Shader.PropertyToID("_TempRefraction_");
            var desc = target.desc.basicDesc;
            desc.enableRandomWrite = true;
            cb.GetTemporaryRT(tempRef, desc);

            cb.SetRayTracingAccelerationStructure(rtShader, "_RaytracingAccelerationStructure", context.defaultAcc);
            cb.SetRayTracingShaderPass(rtShader, "RTGI");
            cb.SetRayTracingTextureParam(rtShader, "_TempResult", tempRef);
            cb.DispatchRays(rtShader, "Refraction", (uint)desc.width, (uint)desc.height, 1, context.camera);

            cb.CopyTexture(tempRef, target);

            cb.ReleaseTemporaryRT(tempRef);
        }
    }
}