using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class Atmo : BaseRenderPass
    {
        // parameters
        public Vector2Int TLutResolution = new Vector2Int(128, 128);

        public Vector2Int SkyLutResolution = new Vector2Int(200, 256);

        public Vector2Int MSLutResolution = new Vector2Int(32, 32);

        public Vector3Int VolumeResolution = new Vector3Int(32, 32, 32);

        public float VolumeMaxDepth = 32000;

        // pins
        [NodePin(PinType.In)]
        public LightListPin sunLight = new LightListPin();
        [NodePin(PinType.InOut, true)]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1));

        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        RenderTexture t_table = null;
        RenderTexture sky_table = null;
        RenderTexture multiScatter_table = null;
        RenderTexture volumeScatter_table = null;

        static ComputeShaderWithName volumeScatter = new ComputeShaderWithName("Shaders/Atmo/VolumeScatterLut");
        static MaterialWithName lutMat = new MaterialWithName("Hidden/AtmoLut");

        HRPAtmo atmo;

        bool TestRTChange(ref RenderTexture rt, RenderTextureFormat format, Vector2Int wh)
        {
            if (rt == null || wh.x != rt.width || wh.y != rt.height)
            {
                if (rt != null) rt.Release();
                rt = new RenderTexture(wh.x, wh.y, 0, format, 0);
                rt.wrapMode = TextureWrapMode.Clamp;
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
                rt = new RenderTexture(whd.x, whd.y, 0, format, 0);
                rt.dimension = TextureDimension.Tex3D;
                rt.volumeDepth = whd.z;
                rt.enableRandomWrite = true;
                rt.wrapMode = TextureWrapMode.Clamp;
                rt.Create();
                return true;
            }
            return false;
        }


        public override void Excute(RenderContext context)
        {
            var cb = context.commandBuffer;

            var sun = sunLight.handle.sunLight;
            if (sun == null)
            {
                atmo = null;
                context.commandBuffer.SetGlobalVector("_SunLuminance", Vector3.one * math.pow(10, 4.6f));
                context.commandBuffer.SetGlobalVector("_SunDir", Vector3.up);
            }
            else
            {
                atmo = sun.atmoPreset;
                context.commandBuffer.SetGlobalVector("_SunLuminance", sun.color * sun.radiance * math.pow(10, 4.6f));
                context.commandBuffer.SetGlobalVector("_SunDir", -sun.direction);
            }

            InitLut(cb);

            int tempColor = Shader.PropertyToID("TempColor");
            cb.GetTemporaryRT(tempColor, target.desc.basicDesc);
            cb.Blit(target, tempColor);
            cb.SetGlobalTexture("_DepthTex", depth);
            cb.Blit(tempColor, target, lutMat, 3);
            cb.ReleaseTemporaryRT(tempColor);
        }


        void InitLut(CommandBuffer cb)
        {
            bool regenerate = false;
            TLutResolution.y = TLutResolution.y / 2 * 2 + 1;
            regenerate |= TestRTChange(ref t_table, RenderTextureFormat.ARGBFloat, TLutResolution);
            regenerate |= TestRTChange(ref multiScatter_table, RenderTextureFormat.ARGBFloat, MSLutResolution);
            regenerate |= CheckParameters();


            cb.SetGlobalFloat("_PlanetRadius", radius);
            cb.SetGlobalFloat("_AtmosphereThickness", at);
            cb.SetGlobalVector("_GroundAlbedo", gr);
            cb.SetGlobalVector("_RayleighScatter", raylei * rayleiS);
            cb.SetGlobalFloat("_MeiScatter", meiS);
            cb.SetGlobalFloat("_OZone", oz);
            cb.SetGlobalFloat("_SunAngle", sunAngle);

            cb.SetGlobalFloat("_MultiScatterStrength", ms);
            cb.SetGlobalFloat("_MaxDepth", VolumeMaxDepth);

            cb.SetGlobalVector("_TLutResolution", new Vector4(TLutResolution.x, TLutResolution.y));
            cb.SetGlobalVector("_SLutResolution", new Vector4(SkyLutResolution.x, SkyLutResolution.y));

            if (regenerate)
            {
                t_table.wrapMode = TextureWrapMode.Clamp;
                cb.Blit(null, t_table, lutMat, 0);
                cb.SetGlobalTexture("T_table", t_table);
                cb.Blit(null, multiScatter_table, lutMat, 1);
                cb.SetGlobalTexture("MS_table", multiScatter_table);
            }

            if (TestRTChange(ref sky_table, RenderTextureFormat.ARGBFloat, SkyLutResolution))
            {
                sky_table.wrapModeU = TextureWrapMode.Repeat;
                sky_table.wrapModeV = TextureWrapMode.Clamp;
            }

            TestRTChange(ref volumeScatter_table, RenderTextureFormat.ARGB32, VolumeResolution);

            cb.Blit(null, sky_table, lutMat, 2);
            cb.SetComputeTextureParam(volumeScatter, 0, "_Result", volumeScatter_table);
            Vector3Int size = VolumeResolution;
            cb.SetComputeVectorParam(volumeScatter, "_Size", new Vector4(VolumeResolution.x, VolumeResolution.y, VolumeResolution.z));
            size.x = size.x / 4 + (size.x % 4 != 0 ? 1 : 0);
            size.y = size.y / 4 + (size.y % 4 != 0 ? 1 : 0);
            size.z = size.z / 4 + (size.z % 4 != 0 ? 1 : 0);
            cb.DispatchCompute(volumeScatter, 0, size.x, size.y, size.z);
            cb.SetGlobalTexture("Volume_table", volumeScatter_table);
            cb.SetGlobalTexture("S_table", sky_table);
        }


        float radius = -1;
        float at = -1;
        Color gr = Color.clear;
        Color raylei = Color.clear;
        float rayleiS = 0;
        float meiS = 0;
        float oz;
        float sunAngle;
        float ms;
        bool CheckParameters()
        {
            bool changed = false;
            if (atmo == null) return changed;
            if (atmo.planetRadius != radius)
            {
                changed = true;
                radius = atmo.planetRadius;
            }
            if (atmo.AtmosphereThickness != at)
            {
                changed = true;
                at = atmo.AtmosphereThickness;
            }
            if (atmo.GroundAlbedo != gr)
            {
                changed = true;
                gr = atmo.GroundAlbedo;
            }
            if (atmo.RayleighScatter != raylei)
            {
                changed = true;
                raylei = atmo.RayleighScatter;
            }
            if (atmo.RayleighScatterStrength != rayleiS)
            {
                changed = true;
                rayleiS = atmo.RayleighScatterStrength;
            }
            if (atmo.MieScatterStrength != meiS)
            {
                changed = true;
                meiS = atmo.MieScatterStrength;
            }
            if (atmo.OzoneStrength != oz)
            {
                changed = true;
                oz = atmo.OzoneStrength;
            }
            if (atmo.sunSolidAngle != sunAngle)
            {
                changed = true;
                sunAngle = atmo.sunSolidAngle;
            }
            if (atmo.MultiScatterStrength != ms)
            {
                changed = true;
                ms = atmo.MultiScatterStrength;
            }

            return changed;
        }
    }
}