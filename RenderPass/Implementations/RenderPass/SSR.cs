using HypnosRenderPipeline.Tools;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class SSR : BaseRenderPass
    {
        [NodePin(PinType.In, true)]
        public TexturePin sceneColor = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Default));

        [NodePin(PinType.In)]
        public TexturePin filteredColor = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Default));

        [NodePin(PinType.In, true)]
        public TexturePin hiZ = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.RFloat, 24), colorCastMode: ColorCastMode.Fixed);

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

        [NodePin(PinType.Out)]
        public TexturePin result;

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


        public override void Execute(RenderContext context)
        {
            var cb = context.commandBuffer;
            //int rt0 = Shader.PropertyToID("RT0");
            //int rt1 = Shader.PropertyToID("RT1");


            //cb.GetTemporaryRT(rt0, result.desc.basicDesc);
            //cb.GetTemporaryRT(rt1, result.desc.basicDesc);

            //cb.SetRenderTarget(rt0);
            //cb.ClearRenderTarget(false,true, Color.red);
            //cb.SetRenderTarget(rt1);
            //cb.ClearRenderTarget(false, true, Color.blue);

            //cb.Blit(rt0, result);

            //cb.ReleaseTemporaryRT(rt0);
            //cb.ReleaseTemporaryRT(rt1);

            if (filteredColor.connected)
                cb.SetGlobalTexture("_FilteredColor", filteredColor);
            else
                cb.SetGlobalTexture("_FilteredColor", sceneColor);

            cb.SetGlobalTexture("_SceneColor", sceneColor);
            cb.SetGlobalTexture("_HiZDepthTex", hiZ);

            cb.SetGlobalTexture("_SkyBox", skybox);

            int2 wh = new int2(sceneColor.desc.basicDesc.width, sceneColor.desc.basicDesc.height);

            cb.SetGlobalVector("_WH", new Vector4(wh.x, wh.y, 1.0f / wh.x, 1.0f / wh.y));


            cb.SetGlobalTexture("_Sobol", bnsLoader.tex_sobol);
            cb.SetGlobalTexture("_ScramblingTile", bnsLoader.tex_scrambling);
            cb.SetGlobalTexture("_RankingTile", bnsLoader.tex_rankingTile);

            int tempRef = Shader.PropertyToID("_TempReflection");
            var desc = result.desc.basicDesc;
            desc.enableRandomWrite = true;
            desc.colorFormat = RenderTextureFormat.ARGBHalf;
            cb.GetTemporaryRT(tempRef, desc);
            cb.SetComputeTextureParam(ssr, 0, "_TempResult", tempRef);
            cb.DispatchCompute(ssr, 0, wh.x / 8 + (wh.x % 8 != 0 ? 1 : 0), wh.y / 8 + (wh.y % 8 != 0 ? 1 : 0), 1);

            RenderTexture his0 = context.resourcesPool.GetTexture(Shader.PropertyToID("_History0"), desc);
            RenderTexture his1 = context.resourcesPool.GetTexture(Shader.PropertyToID("_History1"), desc);

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
            cb.SetComputeTextureParam(ssr, CSPass.TTFilter, "_TempResult", tempRef);
            cb.SetComputeTextureParam(ssr, CSPass.TTFilter, "_Result", result);
            cb.DispatchCompute(ssr, CSPass.TTFilter, dispatchSize.x, dispatchSize.y, 1);

            cb.CopyTexture(result, his0);

            DispatchSpatialFilter(cb, 0.9f, 0.95f);
            DispatchSpatialFilter(cb, 0.85f, 0.9f);
            DispatchSpatialFilter(cb, 0.75f, 0.85f);
            DispatchSpatialFilter(cb, 0.6f, 0.75f);
            DispatchSpatialFilter(cb, 0.45f, 0.6f);
            DispatchSpatialFilter(cb, 0.2f, 0.45f);
            DispatchSpatialFilter(cb, 0, 0.2f);

            cb.SetComputeTextureParam(ssr, CSPass.FinalSynthesis, "_Result", result);
            cb.DispatchCompute(ssr, CSPass.FinalSynthesis, dispatchSize.x, dispatchSize.y, 1);

            cb.ReleaseTemporaryRT(tempRef);
        }

        void DispatchSpatialFilter(CommandBuffer cb, float lowSmooth, float highSmooth)
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