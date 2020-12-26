using HypnosRenderPipeline.Tools;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class RTSpecular : BaseRenderPass
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

        [NodePin(PinType.In)]
        public TexturePin filteredColor = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Default));

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
        public TexturePin ao = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In)]
        public TexturePin skybox = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Default) { dimension = TextureDimension.Cube }, sizeScale: SizeScale.Custom);

        public bool useRTShadow = false;

        ComputeBuffer blockBuffer;
        ComputeBuffer blockBuffer_;
        ComputeBuffer argsBuffer;
        ComputeBuffer argsBuffer_;
        int[] clearArray = new int[3] { 0, 1, 1 };

        static ComputeShaderWithName ssr = new ComputeShaderWithName("Shaders/SSR/SSR");

        static RTShaderWithName rtShader = new RTShaderWithName("Shaders/Tools/RTSpec");

        static BNSLoader bnsLoader = BNSLoader.instance;

        List<LightStructGPU> lightBufferCPU;

        struct CSPass
        {
            public static int Trace = 0;
            public static int RemoveFlare = 1;
            public static int TTFilter = 2;
            public static int SFilter = 3;
            public static int SFilterIndirect = 4;
            public static int FinalSynthesis = 5;
        }

        public RTSpecular()
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
            var cb = context.commandBuffer;

            if (filteredColor.connected)
                cb.SetGlobalTexture("_FilteredColor", filteredColor);
            else
                cb.SetGlobalTexture("_FilteredColor", target);

            cb.SetGlobalTexture("_SceneColor", target);
            cb.SetGlobalTexture("_HiZDepthTex", depth);

            cb.SetGlobalTexture("_SkyBox", skybox);

            var desc = target.desc.basicDesc;
            desc.enableRandomWrite = true;
            int result = Shader.PropertyToID("_SSRResult");
            cb.GetTemporaryRT(result, desc);

            int2 wh = new int2(desc.width, desc.height);

            cb.SetGlobalVector("_WH", new Vector4(wh.x, wh.y, 1.0f / wh.x, 1.0f / wh.y));
            cb.SetGlobalInt("_UseRTShadow", useRTShadow ? 1 : 0);
            

            cb.SetGlobalTexture("_Sobol", bnsLoader.tex_sobol);
            cb.SetGlobalTexture("_ScramblingTile", bnsLoader.tex_scrambling);
            cb.SetGlobalTexture("_RankingTile", bnsLoader.tex_rankingTile);

            int tempRef = Shader.PropertyToID("_TempReflection");
            cb.GetTemporaryRT(tempRef, desc);

            var acc = RTRegister.AccStruct();
#if UNITY_2020_2_OR_NEWER
            acc.Build();
#else
            acc.Update();
#endif
            cb.SetRayTracingAccelerationStructure(rtShader, "_RaytracingAccelerationStructure", acc);
            cb.SetRayTracingShaderPass(rtShader, "RTGI");
            cb.SetRayTracingTextureParam(rtShader, "_TempResult", tempRef);
            cb.DispatchRays(rtShader, "Specular", (uint)desc.width, (uint)desc.height, 1, context.camera);

            RenderTexture his0 = context.resourcesPool.GetTexture(Shader.PropertyToID("_RTSpec_History0"), desc);
            desc.colorFormat = RenderTextureFormat.RFloat;
            RenderTexture hisDepth0 = context.resourcesPool.GetTexture(Shader.PropertyToID("_RTSpec_HistoryDepth0"), desc);

            int2 dispatchSize = new int2(wh.x / 8 + (wh.x % 8 != 0 ? 1 : 0), wh.y / 8 + (wh.y % 8 != 0 ? 1 : 0));
            if (blockBuffer.count != dispatchSize.x * dispatchSize.y)
            {
                blockBuffer.Release();
                blockBuffer = new ComputeBuffer(dispatchSize.x * dispatchSize.y, 4);
                blockBuffer_.Release();
                blockBuffer_ = new ComputeBuffer(dispatchSize.x * dispatchSize.y, 4);
            }


            cb.SetComputeBufferData(argsBuffer, clearArray);
            cb.SetComputeTextureParam(ssr, CSPass.RemoveFlare, "_History", his0);
            cb.SetComputeTextureParam(ssr, CSPass.RemoveFlare, "_TempResult", tempRef);
            cb.DispatchCompute(ssr, CSPass.RemoveFlare, dispatchSize.x, dispatchSize.y, 1);

            cb.SetGlobalVector("_SmoothRange", new Vector4(0.95f, 1f));
            cb.SetComputeBufferData(argsBuffer, clearArray);
            cb.SetComputeTextureParam(ssr, CSPass.SFilter, "_Result", tempRef);
            cb.SetComputeBufferParam(ssr, CSPass.SFilter, "_Indirect", argsBuffer);
            cb.SetComputeBufferParam(ssr, CSPass.SFilter, "_NextBlock", blockBuffer);
            cb.DispatchCompute(ssr, CSPass.SFilter, dispatchSize.x, dispatchSize.y, 1);

            cb.SetComputeTextureParam(ssr, CSPass.TTFilter, "_History", his0);
            cb.SetComputeTextureParam(ssr, CSPass.TTFilter, "_HistoryDepth", hisDepth0);
            cb.SetComputeTextureParam(ssr, CSPass.TTFilter, "_TempResult", tempRef);
            cb.SetComputeTextureParam(ssr, CSPass.TTFilter, "_Result", result);
            cb.DispatchCompute(ssr, CSPass.TTFilter, dispatchSize.x, dispatchSize.y, 1);

            DispatchSpatialFilter(cb, result, 0.9f, 0.95f);
            DispatchSpatialFilter(cb, result, 0.85f, 0.9f);
            DispatchSpatialFilter(cb, result, 0.75f, 0.85f);
            DispatchSpatialFilter(cb, result, 0.6f, 0.75f);
            DispatchSpatialFilter(cb, result, 0.45f, 0.6f);
            DispatchSpatialFilter(cb, result, 0.2f, 0.45f);
            DispatchSpatialFilter(cb, result, 0, 0.2f);

            cb.CopyTexture(result, his0);

            cb.SetComputeTextureParam(ssr, CSPass.FinalSynthesis, "_Result", result);
            cb.DispatchCompute(ssr, CSPass.FinalSynthesis, dispatchSize.x, dispatchSize.y, 1);

            cb.CopyTexture(result, target);
            cb.Blit(depth, hisDepth0); // from Depth24 to R32

            cb.ReleaseTemporaryRT(tempRef);
        }

        void DispatchSpatialFilter(CommandBuffer cb, int result, float lowSmooth, float highSmooth)
        {
            cb.SetGlobalVector("_SmoothRange", new Vector4(lowSmooth, highSmooth));
            cb.SetComputeBufferData(argsBuffer_, clearArray);
            cb.SetComputeTextureParam(ssr, CSPass.SFilterIndirect, "_Result", result);
            cb.SetComputeBufferParam(ssr, CSPass.SFilterIndirect, "_Block", blockBuffer);
            cb.SetComputeBufferParam(ssr, CSPass.SFilterIndirect, "_Indirect", argsBuffer_);
            cb.SetComputeBufferParam(ssr, CSPass.SFilterIndirect, "_NextBlock", blockBuffer_);
            cb.DispatchCompute(ssr, CSPass.SFilterIndirect, argsBuffer, 0);
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