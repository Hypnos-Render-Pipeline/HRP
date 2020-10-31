using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class Atmo : BaseRenderPass
    {
        // parameters
        public float planetRadius = 6371e3f;
        public float AtmosphereThickness = 8e3f;
        public Color GroundAlbedo = new Color(0.25f, 0.25f, 0.25f);
        public Color RayleighScatter = new Color(0.1752f, 0.40785f, 1f);
        public float RayleighScatterStrength = 1;
        public float MieScatterStrength = 1;
        public float OzoneStrength = 1;
        public float sunSolidAngle = (0.5f / 180.0f * Mathf.PI);

        public float MultiScatterStrength = 1f;

        public Vector2Int TLutResolution = new Vector2Int(128, 128);

        public Vector2Int SkyLutResolution = new Vector2Int(200, 256);

        public Vector2Int MultiScatterLutResolution = new Vector2Int(32, 32);

        public Vector3Int VolumeScatterLutResolution = new Vector3Int(32, 32, 32);

        public float maxDepth = 32000;

        // pins
        [NodePin(PinType.In)]
        public LightListPin sunLight = new LightListPin();
        [NodePin(PinType.InOut, true)]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1));

        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        static MaterialWithName lutMat = new MaterialWithName("Hidden/AtmoLut");

        RenderTexture t_table = null;
        RenderTexture sky_table = null;
        RenderTexture multiScatter_table = null;
        RenderTexture volumeScatter_table = null;

        static ComputeShaderWithName volumeScatter = new ComputeShaderWithName("Shaders/Atmo/VolumeScatterLut");


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
                context.commandBuffer.SetGlobalFloat("_SunLuminance", math.pow(10, 4.6f));
                context.commandBuffer.SetGlobalVector("_SunDir", Vector3.up);
            }
            else
            {
                context.commandBuffer.SetGlobalFloat("_SunLuminance", sun.radiance * math.pow(10, 4.6f));
                context.commandBuffer.SetGlobalVector("_SunDir", -sun.direction);
            }

            InitLut(cb);

            int tempColor = Shader.PropertyToID("TempColor");
            cb.GetTemporaryRT(tempColor, target.desc.basicDesc);
            cb.Blit(target, tempColor);
            cb.SetGlobalTexture("_DepthTex", depth);
            cb.Blit(tempColor, target, lutMat, 3);
            cb.ReleaseTemporaryRT(tempColor);
            context.context.ExecuteCommandBuffer(cb);
            cb.Clear();
        }


        void InitLut(CommandBuffer cb)
        {
            bool regenerate = false;
            regenerate |= TestRTChange(ref t_table, RenderTextureFormat.ARGBFloat, TLutResolution);
            regenerate |= TestRTChange(ref multiScatter_table, RenderTextureFormat.ARGBFloat, MultiScatterLutResolution);
            regenerate |= CheckParameters();


            cb.SetGlobalFloat("_PlanetRadius", radius);
            cb.SetGlobalFloat("_AtmosphereThickness", at);
            cb.SetGlobalVector("_GroundAlbedo", gr);
            cb.SetGlobalVector("_RayleighScatter", raylei * rayleiS);
            cb.SetGlobalFloat("_MeiScatter", meiS);
            cb.SetGlobalFloat("_OZone", oz);
            cb.SetGlobalFloat("_SunAngle", sunAngle);

            cb.SetGlobalFloat("_MultiScatterStrength", ms);
            cb.SetGlobalFloat("_MaxDepth", maxDepth);

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

            TestRTChange(ref volumeScatter_table, RenderTextureFormat.ARGB32, VolumeScatterLutResolution);

            cb.Blit(null, sky_table, lutMat, 2);
            cb.SetComputeTextureParam(volumeScatter, 0, "_Result", volumeScatter_table);
            Vector3Int size = VolumeScatterLutResolution;
            cb.SetComputeVectorParam(volumeScatter, "_Size", new Vector4(VolumeScatterLutResolution.x, VolumeScatterLutResolution.y, VolumeScatterLutResolution.z));
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
            if (planetRadius != radius)
            {
                changed = true;
                radius = planetRadius;
            }
            if (AtmosphereThickness != at)
            {
                changed = true;
                at = AtmosphereThickness;
            }
            if (GroundAlbedo != gr)
            {
                changed = true;
                gr = GroundAlbedo;
            }
            if (RayleighScatter != raylei)
            {
                changed = true;
                raylei = RayleighScatter;
            }
            if (RayleighScatterStrength != rayleiS)
            {
                changed = true;
                rayleiS = RayleighScatterStrength;
            }
            if (MieScatterStrength != meiS)
            {
                changed = true;
                meiS = MieScatterStrength;
            }
            if (OzoneStrength != oz)
            {
                changed = true;
                oz = OzoneStrength;
            }
            if (sunSolidAngle != sunAngle)
            {
                changed = true;
                sunAngle = sunSolidAngle;
            }
            if (MultiScatterStrength != ms)
            {
                changed = true;
                ms = MultiScatterStrength;
            }

            return changed;
        }
    }
}