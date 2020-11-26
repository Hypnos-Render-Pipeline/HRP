using HypnosRenderPipeline.Tools;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

        [NodePin(PinType.In, true)]
        public TexturePin sceneColor = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Default));

        [NodePin(PinType.In)]
        public TexturePin filteredColor = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Default));

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

        [NodePin(PinType.Out)]
        public TexturePin result;

        public bool useRTShadow = false;

        ComputeBuffer blockBuffer;
        ComputeBuffer blockBuffer_;
        ComputeBuffer argsBuffer;
        ComputeBuffer argsBuffer_;
        int[] clearArray = new int[3] { 0, 1, 1 };

        static ComputeShaderWithName denoise = new ComputeShaderWithName("Shaders/RTGI/DiffuseDenoise");

        static RTShaderWithName rtShader = new RTShaderWithName("Shaders/RTGI/RTDiffuse");

        static BNSLoader bnsLoader = BNSLoader.instance;

        List<LightStructGPU> lightBufferCPU;

        public RTGI()
        {
            result = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Default) { enableRandomWrite = true }, srcPin: sceneColor);
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

            if (filteredColor.connected)
                cb.SetGlobalTexture("_FilteredColor", filteredColor);
            else
                cb.SetGlobalTexture("_FilteredColor", sceneColor);

            cb.SetGlobalTexture("_SceneColor", sceneColor);

            cb.SetGlobalTexture("_SkyBox", skybox);

            int2 wh = new int2(sceneColor.desc.basicDesc.width, sceneColor.desc.basicDesc.height);

            cb.SetGlobalVector("_WH", new Vector4(wh.x, wh.y, 1.0f / wh.x, 1.0f / wh.y));
            cb.SetGlobalInt("_UseRTShadow", useRTShadow ? 1 : 0);


            cb.SetGlobalTexture("_Sobol", bnsLoader.tex_sobol);
            cb.SetGlobalTexture("_ScramblingTile", bnsLoader.tex_scrambling);
            cb.SetGlobalTexture("_RankingTile", bnsLoader.tex_rankingTile);

            int tempRef = Shader.PropertyToID("_TempReflection");
            var desc = result.desc.basicDesc;
            desc.enableRandomWrite = true;
            desc.colorFormat = RenderTextureFormat.ARGBHalf;
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

            //cb.Blit(tempRef, result);

            RenderTexture his0 = context.resourcesPool.GetTexture(Shader.PropertyToID("_RTDiffuse_History0"), desc);
            desc.colorFormat = RenderTextureFormat.ARGB32;
            RenderTexture hisNormal0 = context.resourcesPool.GetTexture(Shader.PropertyToID("_RTDiffuse_HistoryNormal0"), desc);
            desc.colorFormat = RenderTextureFormat.RFloat;
            RenderTexture hisDepth0 = context.resourcesPool.GetTexture(Shader.PropertyToID("_RTDiffuse__HistoryDepth0"), desc);
            
            int2 dispatchSize = new int2(wh.x / 8 + (wh.x % 8 != 0 ? 1 : 0), wh.y / 8 + (wh.y % 8 != 0 ? 1 : 0));
            if (blockBuffer.count != dispatchSize.x * dispatchSize.y)
            {
                blockBuffer.Release();
                blockBuffer = new ComputeBuffer(dispatchSize.x * dispatchSize.y, 4);
                blockBuffer_.Release();
                blockBuffer_ = new ComputeBuffer(dispatchSize.x * dispatchSize.y, 4);
            }

            cb.SetGlobalVector("_ProcessRange", new Vector4(0.9f, 1f));
            cb.SetComputeBufferData(argsBuffer, clearArray);
            cb.SetComputeTextureParam(denoise, 1, "_Result", tempRef);
            cb.SetComputeBufferParam(denoise, 1, "_Indirect", argsBuffer);
            cb.SetComputeBufferParam(denoise, 1, "_NextBlock", blockBuffer);
            cb.DispatchCompute(denoise, 1, dispatchSize.x, dispatchSize.y, 1);

            cb.SetComputeTextureParam(denoise, 0, "_History", his0);
            cb.SetComputeTextureParam(denoise, 0, "_HistoryNormal", hisNormal0);
            cb.SetComputeTextureParam(denoise, 0, "_HistoryDepth", hisDepth0); 
            cb.SetComputeTextureParam(denoise, 0, "_TempResult", tempRef);
            cb.SetComputeTextureParam(denoise, 0, "_Result", result);
            cb.DispatchCompute(denoise, 0, dispatchSize.x, dispatchSize.y, 1);

            cb.Blit(normal, hisNormal0);
            cb.Blit(depth, hisDepth0);

            DispatchSpatialFilter(cb, 0.75f, 0.9f);
            DispatchSpatialFilter(cb, 0.6f, 0.75f);
            DispatchSpatialFilter(cb, 0.45f, 0.6f);
            DispatchSpatialFilter(cb, 0.2f, 0.45f);
            DispatchSpatialFilter(cb, 0, 0.2f);

            cb.SetComputeTextureParam(denoise, 3, "_History", his0);
            cb.SetComputeTextureParam(denoise, 3, "_Result", result);
            cb.DispatchCompute(denoise, 3, dispatchSize.x, dispatchSize.y, 1);

            //cb.Blit(tempRef, result);
            cb.ReleaseTemporaryRT(tempRef);
        }

        void DispatchSpatialFilter(CommandBuffer cb, float lowSmooth, float highSmooth)
        {
            cb.SetGlobalVector("_ProcessRange", new Vector4(lowSmooth, highSmooth));
            cb.SetComputeBufferData(argsBuffer_, clearArray);
            cb.SetComputeTextureParam(denoise, 2, "_Result", result);
            cb.SetComputeBufferParam(denoise, 2, "_Block", blockBuffer);
            cb.SetComputeBufferParam(denoise, 2, "_Indirect", argsBuffer_);
            cb.SetComputeBufferParam(denoise, 2, "_NextBlock", blockBuffer_);
            cb.DispatchCompute(denoise, 2, argsBuffer, 0);
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