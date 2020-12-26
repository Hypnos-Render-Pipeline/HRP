using HypnosRenderPipeline.Tools;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class SSR : BaseRenderPass
    {
        [NodePin(PinType.InOut, true)]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGBHalf));

        [NodePin(PinType.In)]
        public TexturePin filteredColor = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGBHalf));

        [NodePin(PinType.In, true)]
        public TexturePin hiZ = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.RFloat, 0), colorCastMode: ColorCastMode.Fixed);

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

        ComputeBuffer blockBuffer;
        ComputeBuffer blockBuffer_;
        ComputeBuffer argsBuffer;
        ComputeBuffer argsBuffer_;
        int[] clearArray = new int[3] { 0, 1, 1 }; 

        static ComputeShaderWithName ssr = new ComputeShaderWithName("Shaders/SSR/SSR");
        static BNSLoader bnsLoader = BNSLoader.instance;

        struct CSPass
        {
            public static int Trace = 0;
            public static int RemoveFlare = 1;
            public static int TTFilter = 2;
            public static int SFilter = 3;
            public static int SFilterIndirect = 4;
            public static int FinalSynthesis = 5;
        }


        public SSR()
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

            var desc = target.desc.basicDesc;
            desc.enableRandomWrite = true;
            int result = Shader.PropertyToID("_SSRResult");
            cb.GetTemporaryRT(result, desc);


            if (filteredColor.connected)
                cb.SetGlobalTexture("_FilteredColor", filteredColor);
            else
                cb.SetGlobalTexture("_FilteredColor", target);

            cb.SetGlobalTexture("_SceneColor", target);
            cb.SetGlobalTexture("_HiZDepthTex", hiZ);

            cb.SetGlobalTexture("_SkyBox", skybox);

            int2 wh = new int2(desc.width, desc.height);

            cb.SetGlobalVector("_WH", new Vector4(wh.x, wh.y, 1.0f / wh.x, 1.0f / wh.y));


            cb.SetGlobalTexture("_Sobol", bnsLoader.tex_sobol);
            cb.SetGlobalTexture("_ScramblingTile", bnsLoader.tex_scrambling);
            cb.SetGlobalTexture("_RankingTile", bnsLoader.tex_rankingTile);

            int tempRef = Shader.PropertyToID("_TempReflection");
            cb.GetTemporaryRT(tempRef, desc);
            cb.SetComputeTextureParam(ssr, 0, "_TempResult", tempRef);
            cb.DispatchCompute(ssr, 0, wh.x / 8 + (wh.x % 8 != 0 ? 1 : 0), wh.y / 8 + (wh.y % 8 != 0 ? 1 : 0), 1);

            RenderTexture his0 = context.resourcesPool.GetTexture(Shader.PropertyToID("_SSR_History0"), desc);
            desc.colorFormat = hiZ.desc.basicDesc.colorFormat;
            RenderTexture hisDepth0 = context.resourcesPool.GetTexture(Shader.PropertyToID("_SSR_HistoryDepth0"), desc);

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
            cb.CopyTexture(hiZ, 0, 0, hisDepth0, 0, 0);

            cb.ReleaseTemporaryRT(result);
            cb.ReleaseTemporaryRT(tempRef);
        }

        void DispatchSpatialFilter(CommandBuffer cb, int result, float lowSmooth, float highSmooth)
        {
            cb.SetGlobalVector("_SmoothRange", new Vector4(lowSmooth, highSmooth));
            cb.SetComputeBufferData(argsBuffer_, clearArray);
            cb.SetComputeTextureParam(ssr, 3, "_Result", result);
            cb.SetComputeBufferParam(ssr, 3, "_Block", blockBuffer);
            cb.SetComputeBufferParam(ssr, 3, "_Indirect", argsBuffer_);
            cb.SetComputeBufferParam(ssr, 3, "_NextBlock", blockBuffer_);
            cb.DispatchCompute(ssr, 3, argsBuffer, 0);
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