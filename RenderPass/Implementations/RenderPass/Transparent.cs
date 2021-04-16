using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class Transparent : BaseRenderPass
    {
        [NodePin(PinType.In, true)]
        public BufferPin<LightStructGPU> lightBuffer = new BufferPin<LightStructGPU>(1);

        [NodePin(PinType.In, true)]
        public BufferPin<uint> tiledLights = new BufferPin<uint>(1);

        [NodePin(PinType.In)]
        public LightListPin lights = new LightListPin();

        [NodePin(PinType.InOut)]
        public BufferPin<LightStructGPU> areaLightBuffer = new BufferPin<LightStructGPU>(1);

        [NodePin(PinType.In)]
        [Tooltip("If has, use filtered screen color for refraction.")]
        public TexturePin filterdScreenColor = new TexturePin(new RenderTextureDescriptor(1, 1));

        [NodePin(PinType.InOut)]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGBHalf));

        [NodePin(PinType.In)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        [Tooltip("Enable Area light for transparent objects are more expensive than opaque objects, please remember that.")]
        public bool renderAreaLight = true;

        static public int tempScreenColor = Shader.PropertyToID("_ScreenColor");

        static Texture2D TransformInv_Diffuse, TransformInv_Specular, AmpDiffAmpSpecFresnel, DiscClip;

        List<LightStructGPU> lightBufferCPU = new List<LightStructGPU>();

        public Transparent()
        {
            AmpDiffAmpSpecFresnel = Resources.Load<Texture2D>("Textures/LTC Lut/AmpDiffAmpSpecFresnel");
            TransformInv_Diffuse = Resources.Load<Texture2D>("Textures/LTC Lut/TransformInv_DisneyDiffuse");
            TransformInv_Specular = Resources.Load<Texture2D>("Textures/LTC Lut/TransformInv_GGX");
            DiscClip = Resources.Load<Texture2D>("Textures/LTC Lut/DiscClip");
        }

        public override void Execute(RenderContext context)
        {
            bool releaseTemp = false;
            if (filterdScreenColor.connected)
            {
                context.commandBuffer.SetGlobalTexture(tempScreenColor, filterdScreenColor);
            }
            else if (target.connected)
            {
                var desc = target.desc.basicDesc;
                desc.useMipMap = true;
                desc.autoGenerateMips = true;
                releaseTemp = true;
                context.commandBuffer.GetTemporaryRT(tempScreenColor, desc, FilterMode.Trilinear);
                context.commandBuffer.Blit(target, tempScreenColor);
                context.commandBuffer.SetGlobalTexture(tempScreenColor, tempScreenColor);
            }
            else
            {
                context.commandBuffer.SetGlobalTexture(tempScreenColor, Texture2D.blackTexture);
            }

            context.commandBuffer.SetRenderTarget(color: target, depth: depth);
            context.commandBuffer.ClearRenderTarget(!depth.connected, !target.connected, Color.clear);

            bool area = false;
            if (renderAreaLight)
            {
                if (lights.connected) {
                    area = true;
                    if (!areaLightBuffer.connected) {
                        areaLightBuffer.ReSize(lights.handle.areas.Count);
                        lightBufferCPU.Clear();
                        foreach (var light in lights.handle.areas)
                        {
                            lightBufferCPU.Add(light.lightStructGPU);
                        }
                        context.commandBuffer.SetBufferData(areaLightBuffer, lightBufferCPU);
                    }
                }
                else if (!areaLightBuffer.connected) {
                    Debug.Log("Area light is enabled for transparent object, but pin \"lights\" and \"areaLightBuffer\" both are not connected.");
                }
                else {
                    area = true;
                }

                if (area)
                {
                    context.commandBuffer.SetGlobalTexture("_AmpDiffAmpSpecFresnel", AmpDiffAmpSpecFresnel);
                    context.commandBuffer.SetGlobalTexture("_TransformInv_Diffuse", TransformInv_Diffuse);
                    context.commandBuffer.SetGlobalTexture("_TransformInv_Specular", TransformInv_Specular);
                    context.commandBuffer.SetGlobalTexture("_DiscClip", DiscClip);

                    context.commandBuffer.SetGlobalTexture("_LightDiffuseTex", Texture2D.whiteTexture);
                    context.commandBuffer.SetGlobalTexture("_LightSpecTex", Texture2D.whiteTexture);

                    context.commandBuffer.SetGlobalBuffer("_AreaLightBuffer", areaLightBuffer);
                }
            }
            context.commandBuffer.SetGlobalInt("_AreaLightCount", area ? areaLightBuffer.desc : 0);

            context.context.ExecuteCommandBuffer(context.commandBuffer);
            context.commandBuffer.Clear();

            var a = new DrawingSettings(new ShaderTagId("Transparent"), new SortingSettings(context.camera));
            var b = FilteringSettings.defaultValue;
            b.renderQueueRange = RenderQueueRange.transparent;

            context.context.DrawRenderers(context.defaultCullingResult, ref a, ref b);

            if (releaseTemp)
                context.commandBuffer.ReleaseTemporaryRT(tempScreenColor);
        }
    }
}