using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline.RenderPass
{
    public class SunAtmo : BaseRenderPass
    {
        // parameters
        public Vector2Int TLutResolution = new Vector2Int(128, 128);

        public Vector2Int SkyLutResolution = new Vector2Int(64, 224);

        public Vector2Int MSLutResolution = new Vector2Int(32, 32);

        public Vector3Int VolumeResolution = new Vector3Int(32, 32, 32);

        public float VolumeMaxDepth = 32000;

        // pins
        [NodePin(PinType.In)]
        public LightListPin sunLight = new LightListPin();

        [NodePin(PinType.InOut, true)]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGBFloat), colorCastMode: ColorCastMode.Fixed);

        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        [NodePin(PinType.In, true)]
        public TexturePin baseColor_roughness = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin normal_metallic = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In)]
        public TexturePin ao = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.Out)]
        public TexturePin skyBox = new TexturePin(new RenderTextureDescriptor(128, 128, RenderTextureFormat.ARGBHalf) { dimension = TextureDimension.Cube, useMipMap = true }, sizeScale: SizeScale.Custom);

        public struct SunLight
        {
            public float3 dir;
            public float angle;
            public float3 color;
            public float padding;
        }

        [NodePin(PinType.Out)]
        public BufferPin<SunLight> sunBuffer = new BufferPin<SunLight>(1);

        RenderTexture t_table = null;
        RenderTexture sky_table = null;
        RenderTexture multiScatter_table = null;
        RenderTexture volumeScatter_table = null;

        HRPAtmo atmo;

        static MaterialWithName lightMat = new MaterialWithName("Hidden/DeferredLighting");
        static SunLight[] sunLightClear = new SunLight[] { new SunLight() { dir = 0, color = 0, angle = 0 } };

        int hash;

        public SunAtmo() { hash = GetHashCode(); }

        public override void Excute(RenderContext context)
        {
            var cb = context.commandBuffer;

            var sun = sunLight.handle.sunLight;
            Color lum;
            Vector3 dir;
            if (sun == null)
            {
                atmo = null;
                lum = Color.white * math.pow(10, 4.6f);
                dir = Vector3.up;
            }
            else
            {
                atmo = sun.atmoPreset;
                lum = sun.color * sun.radiance * math.pow(10, 4.6f);
                dir = -sun.direction;
            }
            if (atmo != null)
            {
                bool LutSizeChanged = InitLut();

                atmo.GenerateLut(hash, cb, t_table, multiScatter_table, lum, dir, LutSizeChanged);

                int tempColor = Shader.PropertyToID("TempColor");
                cb.GetTemporaryRT(tempColor, target.desc.basicDesc);
                cb.Blit(target, tempColor, lightMat, 4);

                atmo.GenerateVolumeSkyTexture(cb, volumeScatter_table, sky_table, VolumeMaxDepth);

                if (sunBuffer.connected)
                    atmo.GenerateSunBuffer(cb, sunBuffer, sun.color * sun.radiance);

                atmo.RenderToRT(cb, tempColor, depth, target);

                if (skyBox.connected) 
                    atmo.RenderToCubeMap(cb, skyBox);

                cb.ReleaseTemporaryRT(tempColor);

                //cb.Blit(skyBox, target, skyBoxMat, 0);
            }
            else
            {
                if (sunBuffer.connected)
                    cb.SetComputeBufferData(sunBuffer, sunLightClear);
            }
        }

        bool TestRTChange(ref RenderTexture rt, RenderTextureFormat format, Vector2Int wh)
        {
            if (rt == null || wh.x != rt.width || wh.y != rt.height)
            {
                if (rt != null) rt.Release();
                rt = new RenderTexture(wh.x, wh.y, 0, format, RenderTextureReadWrite.Linear);
                rt.wrapMode = TextureWrapMode.Clamp;
                rt.filterMode = FilterMode.Bilinear;
                rt.Create();
                return true;
            }
            return false;
        }
        bool TestRTChange(ref RenderTexture rt, RenderTextureFormat format, Vector3Int whd)
        {
            if (rt == null || whd.x != rt.width || whd.y != rt.height || whd.z != rt.volumeDepth)
            {
                if (rt != null) rt.Release();
                rt = new RenderTexture(whd.x, whd.y, 0, format, RenderTextureReadWrite.Linear);
                rt.dimension = TextureDimension.Tex3D;
                rt.volumeDepth = whd.z;
                rt.enableRandomWrite = true;
                rt.wrapMode = TextureWrapMode.Clamp;
                rt.filterMode = FilterMode.Bilinear;
                rt.Create();
                return true;
            }
            return false;
        }

        bool InitLut()
        {
            bool regenerate = false;
            TLutResolution.y = TLutResolution.y / 2 * 2 + 1;
            regenerate |= TestRTChange(ref t_table, RenderTextureFormat.ARGBFloat, TLutResolution);
            regenerate |= TestRTChange(ref multiScatter_table, RenderTextureFormat.ARGBFloat, MSLutResolution);

            if (TestRTChange(ref sky_table, RenderTextureFormat.ARGBFloat, SkyLutResolution))
            {
                sky_table.wrapModeU = TextureWrapMode.Mirror;
                sky_table.wrapModeV = TextureWrapMode.Clamp;
            }

            TestRTChange(ref volumeScatter_table, RenderTextureFormat.RGB111110Float, VolumeResolution);

            return regenerate;
        }
    }
}