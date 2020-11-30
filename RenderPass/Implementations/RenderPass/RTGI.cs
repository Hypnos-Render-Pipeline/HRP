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
    public class RTGI : BaseRenderPass
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
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Default));

        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        [NodePin(PinType.In)]
        public TexturePin motion = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.RGFloat, 0), colorCastMode: ColorCastMode.Fixed);

        [NodePin(PinType.In, true)]
        public TexturePin diffuse = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin normal = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In)]
        public TexturePin skybox = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Default) { dimension = TextureDimension.Cube }, sizeScale: SizeScale.Custom);

        public bool useRTShadow = false;
        [Range(1, 8)]
        public int maxDepth = 1;
        [Range(1, 4)]
        public int spp = 1;

        public bool showVariance = false;

        ComputeBuffer blockBuffer;
        ComputeBuffer blockBuffer_;
        ComputeBuffer argsBuffer;
        ComputeBuffer argsBuffer_;
        int[] clearArray = new int[3] { 0, 1, 1 };

        static ComputeShaderWithName denoise = new ComputeShaderWithName("Shaders/RTGI/DiffuseDenoise");

        static RTShaderWithName rtShader = new RTShaderWithName("Shaders/RTGI/RTDiffuse");

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


        public RTGI()
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


        public override void Excute(RenderContext context)
        {
            var cb = context.commandBuffer;

            cb.SetGlobalTexture("_SceneColor", target);

            cb.SetGlobalTexture("_SkyBox", skybox);

            int2 wh = new int2(target.desc.basicDesc.width, target.desc.basicDesc.height);

            cb.SetGlobalVector("_WH", new Vector4(wh.x, wh.y, 1.0f / wh.x, 1.0f / wh.y));
            cb.SetGlobalInt("_UseRTShadow", useRTShadow ? 1 : 0);


            cb.SetGlobalTexture("_Sobol", bnsLoader.tex_sobol);
            cb.SetGlobalTexture("_ScramblingTile", bnsLoader.tex_scrambling);
            cb.SetGlobalTexture("_RankingTile", bnsLoader.tex_rankingTile);

            int2 halfWH = wh / 2 + math.int2(wh % 2 != 0);
            int2 halfDispatchSize = halfWH / 8 + math.int2(halfWH % 8 != 0);

            int halfIndex = Shader.PropertyToID("_HalfIndexTex");
            var half_depth_desc = depth.desc.basicDesc;
            half_depth_desc.width = halfWH.x;
            half_depth_desc.height = halfWH.y;
            half_depth_desc.enableRandomWrite = true;
            half_depth_desc.colorFormat = RenderTextureFormat.RHalf;
            cb.GetTemporaryRT(halfIndex, half_depth_desc);
            cb.SetComputeTextureParam(denoise, CSPass.DownSampleDepth, "_HalfIndexTex", halfIndex);
            cb.DispatchCompute(denoise, CSPass.DownSampleDepth, halfDispatchSize.x, halfDispatchSize.y, 1);

            int tempRef = Shader.PropertyToID("_TempReflection");
            int tempRef2 = Shader.PropertyToID("_TempReflection2");
            int tempTarget = Shader.PropertyToID("_TempTarget");
            int var0 = Shader.PropertyToID("_TempVariance"); 
             var desc = half_depth_desc;
            desc.colorFormat = RenderTextureFormat.ARGBHalf;
            cb.GetTemporaryRT(tempRef, desc);
            cb.GetTemporaryRT(tempRef2, desc);
            desc.colorFormat = RenderTextureFormat.RHalf; cb.GetTemporaryRT(tempRef2, desc);
            cb.GetTemporaryRT(var0, desc);

            var acc = RTRegister.AccStruct();
#if UNITY_2020_2_OR_NEWER
                        acc.Build();
#else
            acc.Update();
#endif
            cb.SetRayTracingAccelerationStructure(rtShader, "_RaytracingAccelerationStructure", acc);
            cb.SetRayTracingShaderPass(rtShader, "RTGI");
            cb.SetRayTracingTextureParam(rtShader, "_TempResult", tempRef);
            cb.SetRayTracingTextureParam(rtShader, "_HalfIndexTex", halfIndex);
            cb.SetRayTracingIntParam(rtShader, "_MaxDepth", maxDepth);
            cb.SetRayTracingIntParam(rtShader, "_SPP", spp);
            cb.DispatchRays(rtShader, "Diffuse", (uint)desc.width, (uint)desc.height, 1, context.camera);

            desc.width = wh.x;
            desc.height = wh.y;
            desc.colorFormat = RenderTextureFormat.ARGBHalf;
            RenderTexture his0 = context.resourcesPool.GetTexture(Shader.PropertyToID("_RTDiffuse_History0"), desc);
            cb.GetTemporaryRT(tempTarget, desc);
            RenderTexture hisNormal0 = context.resourcesPool.GetTexture(Shader.PropertyToID("_RTDiffuse_HistoryNormal0"), normal.desc.basicDesc);
            desc = depth.desc.basicDesc;
            desc.colorFormat = RenderTextureFormat.RFloat;
            RenderTexture hisDepth0 = context.resourcesPool.GetTexture(Shader.PropertyToID("_RTDiffuse__HistoryDepth0"), desc);

            if (blockBuffer.count != halfDispatchSize.x * halfDispatchSize.y)
            {
                blockBuffer.Release();
                blockBuffer = new ComputeBuffer(halfDispatchSize.x * halfDispatchSize.y, 4);
                blockBuffer_.Release();
                blockBuffer_ = new ComputeBuffer(halfDispatchSize.x * halfDispatchSize.y, 4);
            }

            cb.SetComputeTextureParam(denoise, CSPass.SFilter, "_Variance", var0);
            cb.SetGlobalVector("_ProcessRange", new Vector4(0.9f, 1f));
            cb.SetComputeBufferData(argsBuffer, clearArray);
            cb.SetComputeTextureParam(denoise, CSPass.SFilter, "_Result", tempRef);
            cb.SetComputeBufferParam(denoise, CSPass.SFilter, "_Indirect", argsBuffer);
            cb.SetComputeBufferParam(denoise, CSPass.SFilter, "_NextBlock", blockBuffer);
            cb.SetComputeTextureParam(denoise, CSPass.SFilter, "_HalfIndexTex", halfIndex);
            cb.DispatchCompute(denoise, CSPass.SFilter, halfDispatchSize.x, halfDispatchSize.y, 1);

            cb.SetComputeTextureParam(denoise, CSPass.TTFilter, "_History", his0);
            cb.SetComputeTextureParam(denoise, CSPass.TTFilter, "_HistoryNormal", hisNormal0);
            cb.SetComputeTextureParam(denoise, CSPass.TTFilter, "_HistoryDepth", hisDepth0);
            cb.SetComputeTextureParam(denoise, CSPass.TTFilter, "_TempResult", tempRef);
            cb.SetComputeTextureParam(denoise, CSPass.TTFilter, "_Result", tempRef2);
            cb.SetComputeTextureParam(denoise, CSPass.TTFilter, "_HalfIndexTex", halfIndex);
            cb.DispatchCompute(denoise, CSPass.TTFilter, halfDispatchSize.x, halfDispatchSize.y, 1);

            cb.Blit(normal, hisNormal0);
            cb.Blit(depth, hisDepth0);

            cb.SetComputeTextureParam(denoise, CSPass.SFilterIndirect, "_Variance", var0);
            DispatchSpatialFilter(cb, tempRef2, 0.75f, 0.9f);
            DispatchSpatialFilter(cb, tempRef2, 0.5f, 0.75f);
            //DispatchSpatialFilter(cb, tempRef2, 0.25f, 0.5f);
            //DispatchSpatialFilter(cb, tempRef2, 0, 0.25f);
            //DispatchSpatialFilter(cb, tempRef2, 0.45f, 0.6f);
            //DispatchSpatialFilter(cb, tempRef2, 0.2f, 0.45f);
            //DispatchSpatialFilter(cb, tempRef2, 0, 0.2f);

            int2 dispatchSize = new int2(wh.x / 8 + (wh.x % 8 != 0 ? 1 : 0), wh.y / 8 + (wh.y % 8 != 0 ? 1 : 0));
            cb.SetComputeTextureParam(denoise, CSPass.UpSample, "_Result", tempRef2);
            cb.SetComputeTextureParam(denoise, CSPass.UpSample, "_History", his0);
            cb.SetComputeTextureParam(denoise, CSPass.UpSample, "_HalfIndexTex", halfIndex);
            cb.DispatchCompute(denoise, CSPass.UpSample, dispatchSize.x, dispatchSize.y, 1);

            cb.SetComputeTextureParam(denoise, CSPass.FinalSynthesis, "_History", his0);
            cb.SetComputeTextureParam(denoise, CSPass.FinalSynthesis, "_Result", tempTarget);
            cb.DispatchCompute(denoise, CSPass.FinalSynthesis, dispatchSize.x, dispatchSize.y, 1);
            
            if (showVariance)
                cb.Blit(var0, target);
            else
                cb.Blit(tempTarget, target);

            cb.ReleaseTemporaryRT(tempRef);
            cb.ReleaseTemporaryRT(tempRef2);
            cb.ReleaseTemporaryRT(var0);
            cb.ReleaseTemporaryRT(tempTarget);
        }

        void DispatchSpatialFilter(CommandBuffer cb, int target, float lowSmooth, float highSmooth)
        {
            cb.SetGlobalVector("_ProcessRange", new Vector4(lowSmooth, highSmooth));
            cb.SetComputeBufferData(argsBuffer_, clearArray);
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