using HypnosRenderPipeline.Tools;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class ReSTIR : BaseRenderPass
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

        [NodePin(PinType.In, true)]
        public TexturePin diffuse = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin specular = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin normal = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin ao = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In)]
        public TexturePin motion = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.RGFloat, 0), colorCastMode: ColorCastMode.Fixed);

        [NodePin(PinType.In)]
        public TexturePin skybox = new TexturePin(new RenderTextureDescriptor(2, 2, RenderTextureFormat.Default) { dimension = TextureDimension.Cube }, sizeScale: SizeScale.Custom);

        [Range(1, 8)]
        public int maxDepth = 1;

        static RTShaderWithName rtShader = new RTShaderWithName("Shaders/RTGI/ReSTIR");

        static BNSLoader bnsLoader = BNSLoader.instance;

        static ComputeShaderWithName denoise = new ComputeShaderWithName("Shaders/RTGI/ReSTIRDenoise");

        struct CSPass
        {
            public static int DownSampleDepth = 0;
            public static int TTFilter = 1;
            public static int SFilter = 2;
            public static int SFilterIndirect = 3;
            public static int UpSample = 4;
            public static int FinalSynthesis = 5;
            public static int Subtract = 6;
        }

        ComputeBuffer blockBuffer;
        ComputeBuffer blockBuffer_;
        ComputeBuffer argsBuffer;
        ComputeBuffer argsBuffer_;
        int[] clearArray = new int[3] { 0, 1, 1 };

        public ReSTIR()
        {
            argsBuffer = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
            argsBuffer_ = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
            blockBuffer = new ComputeBuffer(1, sizeof(int));
            blockBuffer_ = new ComputeBuffer(1, sizeof(int));
        }
        public override void Dispose()
        {
            argsBuffer.Release();
            argsBuffer_.Release();
            blockBuffer.Release();
            blockBuffer_.Release();
        }

        public override void Execute(RenderContext context)
        {
            //return;
            var cb = context.commandBuffer;

            if (!sun.connected)
            {
                cb.SetBufferData(sun, SunAtmo.SunLight.sunLightClear);
            }

            cb.SetGlobalTexture("_SceneColor", target);

            cb.SetGlobalTexture("_SkyBox", skybox);

            int2 wh = new int2(target.desc.basicDesc.width, target.desc.basicDesc.height);

            cb.SetGlobalVector("_WH", new Vector4(wh.x, wh.y, 1.0f / wh.x, 1.0f / wh.y));

            cb.SetGlobalTexture("_Sobol", bnsLoader.tex_sobol);
            cb.SetGlobalTexture("_ScramblingTile", bnsLoader.tex_scrambling);
            cb.SetGlobalTexture("_RankingTile", bnsLoader.tex_rankingTile);

            var acc = context.defaultAcc;

            var desc = target.desc.basicDesc;
            desc.enableRandomWrite = true;
            RenderTexture his_color = context.resourcesPool.GetTexture(Shader.PropertyToID("_ReSTIR_History"), desc);
            RenderTexture his_color2 = context.resourcesPool.GetTexture(Shader.PropertyToID("_ReSTIR_History2"), desc);
            RenderTexture his_acc = context.resourcesPool.GetTexture(Shader.PropertyToID("_ReSTIR_HistoryAcc"), desc);
            int his_acc2 = Shader.PropertyToID("_HistoryAccTemp");
            cb.GetTemporaryRT(his_acc2, desc);
            desc.colorFormat = RenderTextureFormat.RFloat;
            desc.enableRandomWrite = false;
            RenderTexture his_depth = context.resourcesPool.GetTexture(Shader.PropertyToID("_ReSTIR_HistoryDepth"), desc);
            desc.colorFormat = normal.desc.basicDesc.colorFormat;
            RenderTexture hisNormal = context.resourcesPool.GetTexture(Shader.PropertyToID("_ReSTIR_HistoryNormal"), normal.desc.basicDesc);
            int2 halfWH = wh;
            int2 halfDispatchSize = halfWH / 8 + math.int2(halfWH % 8 != 0);

            if (blockBuffer.count != halfDispatchSize.x * halfDispatchSize.y)
            {
                blockBuffer.Release();
                blockBuffer = new ComputeBuffer(halfDispatchSize.x * halfDispatchSize.y, 4);
                blockBuffer_.Release();
                blockBuffer_ = new ComputeBuffer(halfDispatchSize.x * halfDispatchSize.y, 4);
            }

            //int halfIndex = Shader.PropertyToID("_HalfIndexTex");
            var half_desc = depth.desc.basicDesc;
            half_desc.width = halfWH.x;
            half_desc.height = halfWH.y;
            half_desc.enableRandomWrite = true;
            half_desc.colorFormat = RenderTextureFormat.RHalf;
            //cb.GetTemporaryRT(halfIndex, half_desc);

            half_desc.colorFormat = RenderTextureFormat.ARGBInt;
            RenderTexture temporal_reservoir = context.resourcesPool.GetTexture(Shader.PropertyToID("_ReSTIR_Reservoir0"), half_desc);
            RenderTexture spatial_reservoir = context.resourcesPool.GetTexture(Shader.PropertyToID("_ReSTIR_Reservoir1"), half_desc);
            int spatial_reservoir2 = Shader.PropertyToID("_ReSTIR_Reservoir2");
            cb.GetTemporaryRT(spatial_reservoir2, half_desc);

            half_desc.colorFormat = target.desc.basicDesc.colorFormat;
            int tempTarget = Shader.PropertyToID("_TempTarget");
            cb.GetTemporaryRT(tempTarget, half_desc);

            //cb.SetComputeTextureParam(denoise, CSPass.DownSampleDepth, "_HalfIndexTex", halfIndex);
            //cb.DispatchCompute(denoise, CSPass.DownSampleDepth, halfDispatchSize.x, halfDispatchSize.y, 1);


            cb.SetRayTracingAccelerationStructure(rtShader, "_RaytracingAccelerationStructure", acc);
            cb.SetRayTracingShaderPass(rtShader, "RTGI");
            cb.SetGlobalVector("_HalfWH", new Vector4(half_desc.width, half_desc.height, 1.0f / half_desc.width, 1.0f / half_desc.height));
            cb.SetRayTracingTextureParam(rtShader, "_History", his_color);
            //cb.SetRayTracingTextureParam(rtShader, "_HalfIndexTex", halfIndex);
            cb.SetRayTracingTextureParam(rtShader, "_HistoryDepth", his_depth);
            cb.SetRayTracingTextureParam(rtShader, "_TReservoir", temporal_reservoir);
            cb.SetRayTracingTextureParam(rtShader, "_SReservoir", spatial_reservoir);
            cb.SetRayTracingTextureParam(rtShader, "_SReservoir2", spatial_reservoir2);
            cb.SetRayTracingTextureParam(rtShader, "_TempResult", tempTarget);
            cb.SetRayTracingConstantBufferParam(rtShader, "_Sun", sun, 0, SunAtmo.SunLight.size);
            //if (!motion.connected)
            //    cb.SetGlobalTexture("_MotionTex", Texture2D.blackTexture);
            cb.SetRayTracingIntParam(rtShader, "_MaxDepth", maxDepth);
            cb.DispatchRays(rtShader, "PT0", (uint)half_desc.width, (uint)half_desc.height, 1, context.camera);
            cb.DispatchRays(rtShader, "PT1", (uint)desc.width, (uint)desc.height, 1, context.camera);
            cb.CopyTexture(spatial_reservoir2, spatial_reservoir);
            cb.ReleaseTemporaryRT(spatial_reservoir2);

            cb.SetComputeTextureParam(denoise, CSPass.Subtract, "_History", his_color2);
            cb.SetComputeTextureParam(denoise, CSPass.Subtract, "_Result", tempTarget);
            cb.SetComputeTextureParam(denoise, CSPass.Subtract, "_HisAcc", his_acc);
            cb.SetComputeTextureParam(denoise, CSPass.Subtract, "_HisAccTemp", his_acc2); 
            cb.DispatchCompute(denoise, CSPass.Subtract, halfDispatchSize.x, halfDispatchSize.y, 1);
            cb.CopyTexture(his_acc2, his_acc);
            cb.ReleaseTemporaryRT(his_acc2);

            int tempTarget2 = Shader.PropertyToID("_TempTarget2");
            cb.GetTemporaryRT(tempTarget2, half_desc);

            cb.SetComputeTextureParam(denoise, CSPass.TTFilter, "_History", his_color2);
            cb.SetComputeTextureParam(denoise, CSPass.TTFilter, "_HistoryDepth", his_depth);
            cb.SetComputeTextureParam(denoise, CSPass.TTFilter, "_HistoryNormal", hisNormal);
            cb.SetComputeTextureParam(denoise, CSPass.TTFilter, "_TempResult", tempTarget); 
            cb.SetComputeTextureParam(denoise, CSPass.TTFilter, "_Result", tempTarget2);
            cb.SetComputeTextureParam(denoise, CSPass.TTFilter, "_HisAcc", his_acc);
            //cb.SetComputeTextureParam(denoise, CSPass.TTFilter, "_HalfIndexTex", halfIndex);
            cb.DispatchCompute(denoise, CSPass.TTFilter, halfDispatchSize.x, halfDispatchSize.y, 1);


            cb.SetGlobalVector("_ProcessRange", new Vector4(0.85f, 1f));
            cb.SetBufferData(argsBuffer, clearArray);
            cb.SetComputeTextureParam(denoise, CSPass.SFilter, "_Result", tempTarget2);
            cb.SetComputeBufferParam(denoise, CSPass.SFilter, "_Indirect", argsBuffer);
            cb.SetComputeBufferParam(denoise, CSPass.SFilter, "_NextBlock", blockBuffer);
            //cb.SetComputeTextureParam(denoise, CSPass.SFilter, "_HalfIndexTex", halfIndex);
            cb.DispatchCompute(denoise, CSPass.SFilter, halfDispatchSize.x, halfDispatchSize.y, 1);

            DispatchSpatialFilter(cb, tempTarget2, 0.65f, 0.85f);
            DispatchSpatialFilter(cb, tempTarget2, 0.2f, 0.4f);
            DispatchSpatialFilter(cb, tempTarget2, 0.0f, 0.2f);

            int2 dispatchSize = new int2(wh.x / 8 + (wh.x % 8 != 0 ? 1 : 0), wh.y / 8 + (wh.y % 8 != 0 ? 1 : 0));

            cb.Blit(tempTarget2, his_color2);

            cb.SetComputeTextureParam(denoise, CSPass.FinalSynthesis, "_History", his_color2);
            cb.SetComputeTextureParam(denoise, CSPass.FinalSynthesis, "_Result", his_color);
            cb.DispatchCompute(denoise, CSPass.FinalSynthesis, dispatchSize.x, dispatchSize.y, 1);

            cb.Blit(his_color, target);
            cb.Blit(depth, his_depth);
            cb.CopyTexture(normal, hisNormal);

            //cb.ReleaseTemporaryRT(halfIndex);
            cb.ReleaseTemporaryRT(tempTarget);
            cb.ReleaseTemporaryRT(tempTarget2);
        }

        void DispatchSpatialFilter(CommandBuffer cb, int target, float lowSmooth, float highSmooth)
        {
            cb.SetGlobalVector("_ProcessRange", new Vector4(lowSmooth, highSmooth));
            cb.SetBufferData(argsBuffer_, clearArray);
            cb.SetComputeTextureParam(denoise, CSPass.SFilterIndirect, "_Result", target);
            cb.SetComputeBufferParam(denoise, CSPass.SFilterIndirect, "_Block", blockBuffer);
            cb.SetComputeBufferParam(denoise, CSPass.SFilterIndirect, "_Indirect", argsBuffer_);
            cb.SetComputeBufferParam(denoise, CSPass.SFilterIndirect, "_NextBlock", blockBuffer_);
            cb.DispatchCompute(denoise, CSPass.SFilterIndirect, argsBuffer, 0);
            SwapBuffer(ref argsBuffer, ref argsBuffer_);
            SwapBuffer(ref blockBuffer, ref blockBuffer_);
        }

        void SwapBuffer(ref ComputeBuffer a, ref ComputeBuffer b)
        {
            var c = a;
            a = b;
            b = c;
        }
    }
}