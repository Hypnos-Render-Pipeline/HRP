using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline
{
    public class HRPAtmo : ScriptableObject
    {
        [Min(10000)]
        public float planetRadius = 6371e3f;
        [Min(100)]
        public float AtmosphereThickness = 8e3f;

        [Range(0.01f, 100f)]
        public float brightness = 1;

        public Color GroundAlbedo = new Color(0.25f, 0.25f, 0.25f);
        public Color RayleighScatter = new Color(0.1752f, 0.466f, 1f);
        [Min(0)]
        public float RayleighScatterStrength = 1;
        [Min(0)]
        public float MieScatterStrength = 1;
        [Min(0)]
        public float OzoneStrength = 1;
        [Range(0.0001f, 0.03f)]
        public float sunSolidAngle = (0.5f / 180.0f * Mathf.PI);
        [Min(0)]
        public float MultiScatterStrength = 1f;

        public bool RenderGround = false;

        public Texture2D spaceMap;

        public enum Quality { none, low, medium, high, utra }

        [Header("Clouds")]
        public Quality quality = Quality.none;

        [Range(0, 1)]
        public float CloudCoverage = 0.5f;
        [Range(0.01f, 1)]
        public float CloudDensity = 0.5f;

        public Texture2D cloudMap;
        [Range(0.1f, 100)]
        public float cloudMapScale = 1;

        public Texture2D highCloudMap;

        static ComputeShaderWithName volumeScatter = new ComputeShaderWithName("Shaders/Atmo/VolumeScatterLut");
        static MaterialWithName lutMat = new MaterialWithName("Hidden/AtmoLut");



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

        HashSet<int> rendererIDs = new HashSet<int>();

        /// <summary>
        /// Generate and set Luts
        /// </summary>
        /// <param name="rendererID">a hash value for the renderer, you can generate it by any hash function</param>
        /// <param name="cb"></param>
        /// <param name="T"></param>
        /// <param name="MS"></param>
        /// <param name="sunLum"></param>
        /// <param name="sunDir"></param>
        /// <param name="forceRegenerate">Force regenerate Lut, use it when the Lut size has changed</param>
        public bool GenerateLut(int rendererID, CommandBuffer cb, RenderTexture T, RenderTexture MS, Color sunLum, Vector3 sunDir, bool forceRegenerate = false)
        {
            if (CheckParameters())
            {
                rendererIDs.Clear();
            }

            cb.SetGlobalVector("_SunLuminance", sunLum);
            cb.SetGlobalVector("_SunDir", sunDir);

            cb.SetGlobalFloat("_PlanetRadius", planetRadius);
            cb.SetGlobalFloat("_AtmosphereThickness", AtmosphereThickness);
            cb.SetGlobalVector("_GroundAlbedo", GroundAlbedo);
            cb.SetGlobalVector("_RayleighScatter", RayleighScatter * RayleighScatterStrength);
            cb.SetGlobalFloat("_MeiScatter", MieScatterStrength);
            cb.SetGlobalFloat("_OZone", OzoneStrength);
            cb.SetGlobalFloat("_SunAngle", sunSolidAngle);

            cb.SetGlobalFloat("_MultiScatterStrength", MultiScatterStrength);

            cb.SetGlobalVector("_TLutResolution", new Vector4(T.width, T.height));

            cb.SetGlobalFloat("_Multiplier", brightness);

            bool regenerated = false;
            if (forceRegenerate || !rendererIDs.Contains(rendererID))
            {
                regenerated = true;
                rendererIDs.Add(rendererID);
                cb.Blit(null, T, lutMat, 0);
                cb.SetGlobalTexture("T_table", T);
                cb.Blit(null, MS, lutMat, 1);
            }
            else
                cb.SetGlobalTexture("T_table", T);
            cb.SetGlobalTexture("MS_table", MS);
            return regenerated;
        }

        /// <summary>
        /// Generate Luts associated with camera, will use the preset camera parameters(_ProjectionParams, _ScreenParams, unity_CameraProjection, unity_CameraToWorld, _WorldSpaceCameraPos). 
        /// If these value are not set in your pipeline, please set them before call this function.
        /// </summary>
        /// <param name="cb"></param>
        /// <param name="volume"></param>
        /// <param name="sky"></param>
        public void GenerateVolumeSkyTexture(CommandBuffer cb, RenderTexture volume, RenderTexture sky, float maxDepth, int frameIndex = -1)
        {
            cb.SetGlobalFloat("_RenderGround", RenderGround ? 1 : 0);
            cb.SetGlobalVector("_SLutResolution", new Vector4(sky.width, sky.height));
            cb.SetGlobalFloat("_MaxDepth", maxDepth);
            cb.SetGlobalFloat("_AtmoSlice", frameIndex % 4);
            cb.Blit(null, sky, lutMat, 2);
            cb.SetComputeTextureParam(volumeScatter, 0, "_Result", volume);
            Vector3Int size = new Vector3Int(volume.width, volume.height, volume.volumeDepth);
            cb.SetComputeVectorParam(volumeScatter, "_Size", new Vector4(size.x, size.y, size.z));
            size.x = size.x / 4 + (size.x % 4 != 0 ? 1 : 0);
            size.y = size.y / 4 + (size.y % 4 != 0 ? 1 : 0);
            size.z = size.z / 4 + (size.z % 4 != 0 ? 1 : 0);
            cb.DispatchCompute(volumeScatter, 0, size.x, size.y, size.z);
            cb.SetGlobalTexture("Volume_table", volume);
            cb.SetGlobalTexture("S_table", sky);
        }

        /// <summary>
        /// Generate sun buffer
        /// </summary>
        public void GenerateSunBuffer(CommandBuffer cb, ComputeBuffer sunBuffer, Color sunColor)
        {
            cb.SetGlobalVector("_SunColor", sunColor);
            cb.SetComputeBufferParam(volumeScatter, 1, "_Sun_", sunBuffer);
            cb.DispatchCompute(volumeScatter, 1, 1, 1, 1);     
        }

        /// <summary>
        /// Render atmo, will use the preset camera parameters(_ProjectionParams, _ScreenParams, unity_CameraProjection, unity_CameraToWorld, _WorldSpaceCameraPos). 
        /// If these value are not set in your pipeline, please set them before call this function.
        /// </summary>
        public void RenderAtmoToRT(CommandBuffer cb, RenderTargetIdentifier sceneColor, int depth, RenderTargetIdentifier target, bool applyAtmoFog = false)
        {
            cb.SetGlobalTexture("_DepthTex", depth);
            lutMat.material.SetInt("_ApplyAtmoFog", applyAtmoFog ? 1 : 0);
            cb.Blit(sceneColor, target, lutMat, 3);
        }

        /// <summary>
        /// Render atmo, will use the preset camera parameters(_ProjectionParams, _ScreenParams, unity_CameraProjection, unity_CameraToWorld, _WorldSpaceCameraPos). 
        /// If these value are not set in your pipeline, please set them before call this function.
        /// </summary>
        public void RenderAtmoToCubeMap(CommandBuffer cb, int target)
        {
            cb.SetRenderTarget(target, 0, CubemapFace.PositiveX); 
            cb.SetGlobalInt("_Slice", 0);
            cb.Blit(null, BuiltinRenderTextureType.CurrentActive, lutMat, 4);

            cb.SetRenderTarget(target, 0, CubemapFace.NegativeX);
            cb.SetGlobalInt("_Slice", 1);
            cb.Blit(null, BuiltinRenderTextureType.CurrentActive, lutMat, 4);

            cb.SetRenderTarget(target, 0, CubemapFace.PositiveY);
            cb.SetGlobalInt("_Slice", 2);
            cb.Blit(null, BuiltinRenderTextureType.CurrentActive, lutMat, 4);

            cb.SetRenderTarget(target, 0, CubemapFace.NegativeY);
            cb.SetGlobalInt("_Slice", 3);
            cb.Blit(null, BuiltinRenderTextureType.CurrentActive, lutMat, 4);

            cb.SetRenderTarget(target, 0, CubemapFace.PositiveZ);
            cb.SetGlobalInt("_Slice", 4);
            cb.Blit(null, BuiltinRenderTextureType.CurrentActive, lutMat, 4);

            cb.SetRenderTarget(target, 0, CubemapFace.NegativeZ);
            cb.SetGlobalInt("_Slice", 5);
            cb.Blit(null, BuiltinRenderTextureType.CurrentActive, lutMat, 4);
        }

#if UNITY_EDITOR
        static public HRPAtmo Create()
        {
            return HypnosRenderPipeline.FileUtil.SaveAssetInProject<HRPAtmo>();
        }

        [UnityEditor.MenuItem("HypnosRenderPipeline/Atmo/Create Atmo Preset")]
        public static void CreateMenu()
        {
            Create();
        }
#endif
    }
}
