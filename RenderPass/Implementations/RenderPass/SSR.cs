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

        [NodePin(PinType.In, true)]
        public TexturePin baseColor_roughness = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin normal_metallic = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

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


        public override void Excute(RenderContext context)
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

            cb.SetGlobalTexture("_DepthTex", hiZ);
            cb.SetGlobalTexture("_BaseColorTex", baseColor_roughness);
            cb.SetGlobalTexture("_NormalTex", normal_metallic);
            cb.SetGlobalTexture("_SceneColor", sceneColor);

            if (ao.connected)
                cb.SetGlobalTexture("_AOTex", ao);
            else
                cb.SetGlobalTexture("_AOTex", Texture2D.whiteTexture);

            if (motion.connected)
                cb.SetGlobalTexture("_MotionTex", motion);
            else
                cb.SetGlobalTexture("_MotionTex", Texture2D.blackTexture);

            if (filteredColor.connected)
                cb.SetGlobalTexture("_FilteredColor", filteredColor);
            else
                cb.SetGlobalTexture("_FilteredColor", sceneColor);


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
            

            cb.SetComputeTextureParam(ssr, 1, "_History", his0);
            cb.SetComputeTextureParam(ssr, 1, "_TempResult", tempRef);
            cb.SetComputeTextureParam(ssr, 1, "_Result", result);
            cb.DispatchCompute(ssr, 1, dispatchSize.x, dispatchSize.y, 1);
            
            cb.SetGlobalVector("_SmoothRange", new Vector4(0.95f, 1f));
            cb.SetComputeBufferData(argsBuffer, clearArray);
            cb.SetComputeTextureParam(ssr, 2, "_Result", result);
            cb.SetComputeBufferParam(ssr, 2, "_Indirect", argsBuffer);
            cb.SetComputeBufferParam(ssr, 2, "_NextBlock", blockBuffer);
            cb.DispatchCompute(ssr, 2, dispatchSize.x, dispatchSize.y, 1);

            DispatchSpatialFilter(cb, 0.9f, 0.95f);
            DispatchSpatialFilter(cb, 0.85f, 0.9f);
            DispatchSpatialFilter(cb, 0.8f, 0.85f);
            DispatchSpatialFilter(cb, 0.7f, 0.8f);
            DispatchSpatialFilter(cb, 0.6f, 0.7f);
            DispatchSpatialFilter(cb, 0.45f, 0.6f);
            DispatchSpatialFilter(cb, 0.25f, 0.45f);
            DispatchSpatialFilter(cb, 0, 0.25f);

            cb.SetComputeTextureParam(ssr, 4, "_Result", result);
            cb.DispatchCompute(ssr, 4, dispatchSize.x, dispatchSize.y, 1);

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