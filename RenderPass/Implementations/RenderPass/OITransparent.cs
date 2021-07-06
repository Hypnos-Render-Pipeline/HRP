using HypnosRenderPipeline.Tools;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class OITransparent : BaseRenderPass
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

        [NodePin(PinType.InOut, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                                                    SizeCastMode.ResizeToInput,
                                                                    ColorCastMode.Fixed,
                                                                    SizeScale.Full);

        public struct OITOutput {
            float3 SrcColor;
            int alpha;
        };
        public struct OITOutputList { float4 zs; OITOutput data0, data1, data2, data3; };

        [NodePin(PinType.Out)]
        public BufferPin<OITOutputList> oitOutput = new BufferPin<OITOutputList>(1);


        ComputeShaderWithName initOITDepth = new ComputeShaderWithName("Shaders/Tools/ClearOITBuffer");
        MaterialWithName OITBlend = new MaterialWithName("Hidden/OITBlend");

        public override void Execute(RenderContext context)
        {
            var cb = context.commandBuffer;

            var a = new DrawingSettings(new ShaderTagId("Transparent"), new SortingSettings(context.camera) { criteria = SortingCriteria.CommonOpaque });
            a.enableInstancing = true;

            var b = FilteringSettings.defaultValue;
            b.renderQueueRange = RenderQueueRange.transparent;

            var desc = target.desc.basicDesc;
            desc.colorFormat = RenderTextureFormat.RInt;
            desc.enableRandomWrite = true;
            var _LockTex = Shader.PropertyToID("_LockTex");
            cb.GetTemporaryRT(_LockTex, desc);

            oitOutput.ReSize(desc.width * desc.height);

            cb.SetRenderTarget(_LockTex);
            cb.ClearRenderTarget(false, true, Color.clear);

            cb.SetComputeBufferParam(initOITDepth, 0, "_OITOutputList", oitOutput);
            cb.DispatchCompute(initOITDepth, 0, (desc.width * desc.height + 63) / 64, 1, 1);

            cb.SetGlobalInt("_ScreenWidth", desc.width);

            cb.SetRenderTarget(color: target, depth: depth);
            cb.ClearRandomWriteTargets();
            cb.SetRandomWriteTarget(1, _LockTex);
            cb.SetRandomWriteTarget(2, oitOutput);
            cb.SetGlobalTexture("_Lock", _LockTex);
            cb.SetGlobalBuffer("_OITOutputList", oitOutput);

            cb.DrawRenderers(context.defaultCullingResult, ref a, ref b);

            cb.ClearRandomWriteTargets();

            var tmp_color = Shader.PropertyToID("_TempColor");
            cb.GetTemporaryRT(tmp_color, target.desc.basicDesc);
            cb.Blit(target, tmp_color, OITBlend, 0);
            cb.CopyTexture(tmp_color, target);

            cb.ReleaseTemporaryRT(_LockTex);
            cb.ReleaseTemporaryRT(tmp_color);
        }
    }

}