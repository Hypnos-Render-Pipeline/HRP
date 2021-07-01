using Unity.Mathematics;
using static Unity.Mathematics.math;
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

        [Range(8000, 160000)]
        public float CloudShadowDistance = 16000;

        public bool applyAtmoFog = true;

        // pins
        [NodePin(PinType.In)]
        public LightListPin sunLight = new LightListPin();

        [NodePin(PinType.InOut)]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGBHalf));

        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        [NodePin(PinType.In, true)]
        public TexturePin diffuse = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin specular = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin normal = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In)]
        public TexturePin ao = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.Out)]
        public TexturePin skyBox = new TexturePin(new RenderTextureDescriptor(128, 128, RenderTextureFormat.ARGBHalf) { dimension = TextureDimension.Cube, useMipMap = true }, sizeScale: SizeScale.Custom);

        [NodePin(PinType.Out)]
        public TexturePin cloudShadowMap = new TexturePin(new RenderTextureDescriptor(1024, 1024, RenderTextureFormat.ARGBFloat, 0) { enableRandomWrite = true }, sizeScale: SizeScale.Custom);

        [NodePin(PinType.Out)]
        public AfterAtmo afterAtmo = new AfterAtmo();

        public struct SunLight
        {
            public float3 dir;
            public float angle;
            public float3 color;
        }

        [NodePin(PinType.Out)]
        public BufferPin<SunLight> sunBuffer = new BufferPin<SunLight>(1);

        RenderTexture t_table = null;
        RenderTexture sky_table = null;
        RenderTexture multiScatter_table = null;
        RenderTexture volumeScatter_table = null;

        RenderTexture worleyVolume3D;
        RenderTexture worleyPerlinVolume3D;
        Texture2D curlNoise2D;

        Texture2D blueNoise2D;

        struct TextureIDs
        {
            public static int _Depth = Shader.PropertyToID(nameof(_Depth));
            public static int _DownSampled_MinMax_Depth = Shader.PropertyToID(nameof(_DownSampled_MinMax_Depth));
            public static int _Ray_Index = Shader.PropertyToID(nameof(_Ray_Index));
            public static int _SceneColorTex = Shader.PropertyToID(nameof(_SceneColorTex));
            public static int _Marching_Result_A = Shader.PropertyToID(nameof(_Marching_Result_A));
            public static int _History = Shader.PropertyToID(nameof(_History));
            public static int _HistoryDepth = Shader.PropertyToID(nameof(_HistoryDepth));
            public static int _HalfResResult = Shader.PropertyToID(nameof(_HalfResResult));
            public static int _Cloud = Shader.PropertyToID(nameof(_Cloud));
            public static int _ShadowMapTexture = Shader.PropertyToID(nameof(_ShadowMapTexture));
            public static int _CloudMap = Shader.PropertyToID(nameof(_CloudMap));
            public static int T_table = Shader.PropertyToID(nameof(T_table));
            public static int J_table = Shader.PropertyToID(nameof(J_table));

            public static int _HighCloudMap = Shader.PropertyToID(nameof(_HighCloudMap));
            public static int _Volume2D = Shader.PropertyToID(nameof(_Volume2D));
            public static int _Volume3D = Shader.PropertyToID(nameof(_Volume3D));
            public static int _WorleyVolume = Shader.PropertyToID(nameof(_WorleyVolume));
            public static int _WorleyPerlinVolume = Shader.PropertyToID(nameof(_WorleyPerlinVolume));
            public static int _CurlNoise = Shader.PropertyToID(nameof(_CurlNoise));
            public static int _BlueNoise = Shader.PropertyToID(nameof(_BlueNoise));
            public static int _SpaceMap = Shader.PropertyToID(nameof(_SpaceMap));

            public static int _CloudShadowMap = Shader.PropertyToID(nameof(_CloudShadowMap));
            public static int _CloudSM = Shader.PropertyToID(nameof(_CloudSM));            
        }
        struct PropertyIDs
        {
            public static int _WH = Shader.PropertyToID(nameof(_WH));

            public static int _LightTransform = Shader.PropertyToID(nameof(_LightTransform));

            public static int _CloudMat = Shader.PropertyToID(nameof(_CloudMat));
            public static int _CloudMat_Inv = Shader.PropertyToID(nameof(_CloudMat_Inv));

            public static int _CloudMapScale = Shader.PropertyToID(nameof(_CloudMapScale));

            public static int _Quality = Shader.PropertyToID(nameof(_Quality));

            public static int _Brightness = Shader.PropertyToID(nameof(_Brightness));            

            public static int _GapLightIntensity = Shader.PropertyToID(nameof(_GapLightIntensity));
            public static int _CloudCoverage = Shader.PropertyToID(nameof(_CloudCoverage));
            public static int _CloudDensity = Shader.PropertyToID(nameof(_CloudDensity));
            public static int _EnableAurora = Shader.PropertyToID(nameof(_EnableAurora));

            public static int _Size_XAtlas_Y_Atlas = Shader.PropertyToID(nameof(_Size_XAtlas_Y_Atlas));
        }
        enum CloudPass
        {
            DownSampleDepth = 0,
            GetRayIndex,
            MarchRay,
            CheckboardUpsample,
            BlitToHistory,
            FullResolutionUpsample,
            LoadVolumeData,
            WriteCloudShadowMap,
            BlurCloudShadowMap,
        }


        HRPAtmo atmo;


        static ComputeShaderWithName cloudCS = new ComputeShaderWithName("Shaders/Atmo/Cloud");
        static MaterialWithName lightMat = new MaterialWithName("Hidden/DeferredLighting");
        static SunLight[] sunLightClear = new SunLight[] { new SunLight() { dir = 0, color = 0, angle = 0 } };


        int hash;

        public SunAtmo() {
            hash = GetHashCode();

            worleyVolume3D = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            worleyVolume3D.enableRandomWrite = true;
            worleyVolume3D.dimension = TextureDimension.Tex3D;
            worleyVolume3D.volumeDepth = 32;
            worleyVolume3D.wrapMode = TextureWrapMode.Repeat;
            worleyVolume3D.filterMode = FilterMode.Trilinear;
            worleyVolume3D.useMipMap = true;
            worleyVolume3D.autoGenerateMips = false;
            worleyVolume3D.Create();
            worleyPerlinVolume3D = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            worleyPerlinVolume3D.enableRandomWrite = true;
            worleyPerlinVolume3D.dimension = TextureDimension.Tex3D;
            worleyPerlinVolume3D.volumeDepth = 128;
            worleyPerlinVolume3D.wrapMode = TextureWrapMode.Repeat;
            worleyPerlinVolume3D.filterMode = FilterMode.Trilinear;
            worleyPerlinVolume3D.useMipMap = true;
            worleyPerlinVolume3D.autoGenerateMips = false;
            worleyPerlinVolume3D.Create();

            var _WorleyPerlinVolume2D = Resources.Load<Texture2D>("Textures/Cloud Noise/WorleyPerlinVolume");
            var _WorleyVolume2D = Resources.Load<Texture2D>("Textures/Cloud Noise/NoiseErosion");
            CommandBuffer writeVolumecb = new CommandBuffer();
            writeVolumecb.SetComputeTextureParam(cloudCS, (int)CloudPass.LoadVolumeData, TextureIDs._Volume2D, _WorleyPerlinVolume2D);
            writeVolumecb.SetComputeTextureParam(cloudCS, (int)CloudPass.LoadVolumeData, TextureIDs._Volume3D, worleyPerlinVolume3D);
            writeVolumecb.SetComputeVectorParam(cloudCS, PropertyIDs._Size_XAtlas_Y_Atlas, new Vector4(128, 16, 8));
            writeVolumecb.DispatchCompute(cloudCS, (int)CloudPass.LoadVolumeData, 32, 32, 32);
            writeVolumecb.SetComputeTextureParam(cloudCS, (int)CloudPass.LoadVolumeData, TextureIDs._Volume2D, _WorleyVolume2D);
            writeVolumecb.SetComputeTextureParam(cloudCS, (int)CloudPass.LoadVolumeData, TextureIDs._Volume3D, worleyVolume3D);
            writeVolumecb.SetComputeVectorParam(cloudCS, PropertyIDs._Size_XAtlas_Y_Atlas, new Vector4(32, 32, 1));
            writeVolumecb.DispatchCompute(cloudCS, (int)CloudPass.LoadVolumeData, 8, 8, 8);
            Graphics.ExecuteCommandBuffer(writeVolumecb);
            worleyVolume3D.GenerateMips();
            worleyPerlinVolume3D.GenerateMips();

            curlNoise2D = Resources.Load<Texture2D>("Textures/Cloud Noise/Curl");
            blueNoise2D = Resources.Load<Texture2D>("Textures/Cloud Noise/BlueNoise");
        }

        public override void Dispose()
        {
            if (t_table != null) t_table.Release();
            if (sky_table != null) sky_table.Release();
            if (multiScatter_table != null) multiScatter_table.Release();
            if (volumeScatter_table != null) volumeScatter_table.Release();
            if (worleyVolume3D != null) worleyVolume3D.Release();
            if (worleyPerlinVolume3D != null) worleyPerlinVolume3D.Release();
        }

        public override void DisExecute(RenderContext contex)
        {
            afterAtmo.atmo = null;
            contex.commandBuffer.SetBufferData(sunBuffer, sunLightClear);
            contex.commandBuffer.SetGlobalConstantBuffer(sunBuffer, "_Sun", 0, sizeof(float) * 7);
        }

        public override void Execute(RenderContext context)
        {
            var cb = context.commandBuffer;

            if (!target.connected)
            {
                cb.SetRenderTarget(target);
                cb.ClearRenderTarget(false, true, Color.black);
            }

            var sun = sunLight.handle.sunLight;
            Color lum;
            Vector3 dir;
            if (sun == null)
            {
                atmo = null;
                lum = Color.white * math.pow(10, 5.7f);
                dir = Vector3.up;
            }
            else
            {
                atmo = sun.atmoPreset;
                lum = sun.color * sun.radiance * math.pow(10, 5.7f);
                dir = -sun.direction;
            }

            afterAtmo.atmo = atmo;

            if (atmo != null)
            {
                bool LutSizeChanged = InitLut();

                cb.SetGlobalFloat(PropertyIDs._Brightness, atmo.brightness);

                atmo.GenerateLut(hash, cb, t_table, multiScatter_table, lum, dir, LutSizeChanged);

                atmo.GenerateVolumeSkyTexture(cb, volumeScatter_table, sky_table, VolumeMaxDepth, context.frameIndex);

                atmo.GenerateSunBuffer(cb, sunBuffer, sun.color * sun.radiance);

                int tempColor = Shader.PropertyToID("TempColor");
                cb.GetTemporaryRT(tempColor, target.desc.basicDesc);
                cb.CopyTexture(target, tempColor);
                //cb.Blit(target, tempColor, lightMat, 4); // directional sun light

                atmo.RenderAtmoToRT(cb, tempColor, depth, target, applyAtmoFog);

                if (atmo.quality != HRPAtmo.Quality.none)
                {
                    int2 target_WH = new int2(target.desc.basicDesc.width, target.desc.basicDesc.height);
                    Vector4 _WH = new Vector4(target_WH.x, target_WH.y, 1.0f / target_WH.x, 1.0f / target_WH.y);
                    Vector2Int depth_wh = new Vector2Int((int)(_WH.x / 2), (int)(_WH.y / 2));
                    Vector2Int ray_wh = new Vector2Int(depth_wh.x / 2, depth_wh.y / 2);

                    var downSampledMinMaxDepthDesc = new RenderTextureDescriptor();
                    downSampledMinMaxDepthDesc.autoGenerateMips = false;
                    downSampledMinMaxDepthDesc.colorFormat = RenderTextureFormat.RHalf;
                    downSampledMinMaxDepthDesc.enableRandomWrite = true;
                    downSampledMinMaxDepthDesc.depthBufferBits = 0;
                    downSampledMinMaxDepthDesc.msaaSamples = 1;
                    downSampledMinMaxDepthDesc.dimension = TextureDimension.Tex2D;
                    var rayIndexDesc = downSampledMinMaxDepthDesc;
                    rayIndexDesc.colorFormat = RenderTextureFormat.RGFloat;
                    var rgba16Desc = rayIndexDesc;
                    rgba16Desc.colorFormat = target.desc.basicDesc.colorFormat;
                    var r16Desc = new RenderTextureDescriptor(1024, 1024, RenderTextureFormat.RHalf);
                    r16Desc.enableRandomWrite = true;
                    r16Desc.autoGenerateMips = false;
                    r16Desc.depthBufferBits = 0;
                    r16Desc.msaaSamples = 1;
                    r16Desc.dimension = TextureDimension.Tex2D;
                    var hisDesc = new RenderTextureDescriptor(depth_wh.x, depth_wh.y, rgba16Desc.colorFormat) { enableRandomWrite = true };

                    var his = context.resourcesPool.GetTexture(Shader.PropertyToID("SunAtmo_History"), hisDesc);
                    his.filterMode = FilterMode.Point;

                    cb.SetGlobalTexture(TextureIDs._CloudMap, atmo.cloudMap == null ? Texture2D.whiteTexture : atmo.cloudMap);
                    cb.SetGlobalTexture(TextureIDs._HighCloudMap, atmo.highCloudMap == null ? Texture2D.blackTexture : atmo.highCloudMap);
                    cb.SetGlobalTexture(TextureIDs._SpaceMap, atmo.spaceMap);

                    if (atmo.quality == HRPAtmo.Quality.low)
                    {
                        cb.SetGlobalVector(PropertyIDs._Quality, new Vector4(80, 8, 64, 0));
                    }
                    else if (atmo.quality == HRPAtmo.Quality.medium)
                    {
                        cb.SetGlobalVector(PropertyIDs._Quality, new Vector4(40, 12, 128, 0.2f));
                    }
                    else if (atmo.quality == HRPAtmo.Quality.high)
                    {
                        cb.SetGlobalVector(PropertyIDs._Quality, new Vector4(20, 16, 256, 0.66f));
                    }
                    else
                    {
                        cb.SetGlobalVector(PropertyIDs._Quality, new Vector4(10, 24, 512, 1));
                    }

#if UNITY_EDITOR
                    if (worleyVolume3D == null)
                    {
                        worleyVolume3D = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                        worleyVolume3D.enableRandomWrite = true;
                        worleyVolume3D.dimension = TextureDimension.Tex3D;
                        worleyVolume3D.volumeDepth = 32;
                        worleyVolume3D.wrapMode = TextureWrapMode.Repeat;
                        worleyVolume3D.filterMode = FilterMode.Trilinear;
                        worleyVolume3D.useMipMap = true;
                        worleyVolume3D.autoGenerateMips = false;
                        worleyVolume3D.Create();
                        worleyPerlinVolume3D = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                        worleyPerlinVolume3D.enableRandomWrite = true;
                        worleyPerlinVolume3D.dimension = TextureDimension.Tex3D;
                        worleyPerlinVolume3D.volumeDepth = 128;
                        worleyPerlinVolume3D.wrapMode = TextureWrapMode.Repeat;
                        worleyPerlinVolume3D.filterMode = FilterMode.Trilinear;
                        worleyPerlinVolume3D.useMipMap = true;
                        worleyPerlinVolume3D.autoGenerateMips = false;
                        worleyPerlinVolume3D.Create();

                        var _WorleyPerlinVolume2D = Resources.Load<Texture2D>("Textures/Cloud Noise/WorleyPerlinVolume");
                        var _WorleyVolume2D = Resources.Load<Texture2D>("Textures/Cloud Noise/NoiseErosion");
                        CommandBuffer writeVolumecb = new CommandBuffer();
                        writeVolumecb.SetComputeTextureParam(cloudCS, (int)CloudPass.LoadVolumeData, TextureIDs._Volume2D, _WorleyPerlinVolume2D);
                        writeVolumecb.SetComputeTextureParam(cloudCS, (int)CloudPass.LoadVolumeData, TextureIDs._Volume3D, worleyPerlinVolume3D);
                        writeVolumecb.SetComputeVectorParam(cloudCS, PropertyIDs._Size_XAtlas_Y_Atlas, new Vector4(128, 16, 8));
                        writeVolumecb.DispatchCompute(cloudCS, (int)CloudPass.LoadVolumeData, 32, 32, 32);
                        writeVolumecb.SetComputeTextureParam(cloudCS, (int)CloudPass.LoadVolumeData, TextureIDs._Volume2D, _WorleyVolume2D);
                        writeVolumecb.SetComputeTextureParam(cloudCS, (int)CloudPass.LoadVolumeData, TextureIDs._Volume3D, worleyVolume3D);
                        writeVolumecb.SetComputeVectorParam(cloudCS, PropertyIDs._Size_XAtlas_Y_Atlas, new Vector4(32, 32, 1));
                        writeVolumecb.DispatchCompute(cloudCS, (int)CloudPass.LoadVolumeData, 8, 8, 8);
                        Graphics.ExecuteCommandBuffer(writeVolumecb);
                        worleyVolume3D.GenerateMips();
                        worleyPerlinVolume3D.GenerateMips();

                        curlNoise2D = Resources.Load<Texture2D>("Textures/Cloud Noise/Curl");
                        blueNoise2D = Resources.Load<Texture2D>("Textures/Cloud Noise/BlueNoise");
                    }
#endif

                    cb.SetGlobalTexture(TextureIDs._WorleyVolume, worleyVolume3D);
                    cb.SetGlobalTexture(TextureIDs._WorleyPerlinVolume, worleyPerlinVolume3D);
                    cb.SetGlobalTexture(TextureIDs._CurlNoise, curlNoise2D);
                    cb.SetGlobalTexture(TextureIDs._BlueNoise, blueNoise2D);
                    if (atmo.spaceMap != null)
                        cb.SetGlobalTexture(TextureIDs._SpaceMap, atmo.spaceMap);
                    else
                        cb.SetGlobalTexture(TextureIDs._SpaceMap, Texture2D.blackTexture);

                    //cb.SetGlobalTexture(TextureIDs.J_table, j_table);
                    //cb.SetGlobalFloat(Properties._GapLightIntensity, settings.gapLight);
                    cb.SetGlobalFloat(PropertyIDs._CloudCoverage, atmo.CloudCoverage);
                    cb.SetGlobalFloat(PropertyIDs._CloudDensity, atmo.CloudDensity);
                    cb.SetGlobalFloat(PropertyIDs._CloudMapScale, atmo.cloudMapScale);
                    cb.SetGlobalMatrix(PropertyIDs._LightTransform, sun.transform.worldToLocalMatrix);
                    //cb.SetGlobalInt(PropertyIDs._EnableAurora, settings.enableAurora ? 1 : 0);

                    //if (false)
                    {
                        Vector2Int dispatch_size_full = new Vector2Int(target_WH.x / 8 + (target_WH.x % 8 != 0 ? 1 : 0), target_WH.y / 8 + (target_WH.y % 8 != 0 ? 1 : 0));
                        Vector2Int dispatch_size_half = new Vector2Int(depth_wh.x / 8 + (depth_wh.x % 8 != 0 ? 1 : 0), depth_wh.y / 8 + (depth_wh.y % 8 != 0 ? 1 : 0));
                        Vector2Int dispatch_size_quarter = new Vector2Int(ray_wh.x / 8 + (ray_wh.x % 8 != 0 ? 1 : 0), ray_wh.y / 8 + (ray_wh.y % 8 != 0 ? 1 : 0));

                        cb.SetGlobalVector(PropertyIDs._WH, _WH);



                        float3 sunX;
                        if (abs(dir.y) != 1)
                        {
                            sunX = normalize(cross(dir, Vector3.up));
                        }
                        else
                        {
                            sunX = Vector3.left;
                        }

                        float3 sunY = cross(sunX, dir);
                        float3 sunZ = -dir;
                        float3 sunP = context.camera.transform.position;
                        sunP.y = atmo.planetRadius + 3000;

                        float CloudShadowDistance2 = CloudShadowDistance * 2;
                        float4x4 cloudMat_Inv = float4x4(float4(sunX * CloudShadowDistance2, 0), float4(sunY * CloudShadowDistance2, 0), float4(sunZ, 0), float4(sunP - (sunX + sunY) * CloudShadowDistance, 1));
                        float4x4 cloudMat = inverse(cloudMat_Inv);

                        cb.SetGlobalMatrix(PropertyIDs._CloudMat, cloudMat);
                        cb.SetGlobalMatrix(PropertyIDs._CloudMat_Inv, cloudMat_Inv);
                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.WriteCloudShadowMap, TextureIDs._CloudSM, cloudShadowMap);
                        cb.DispatchCompute(cloudCS, (int)CloudPass.WriteCloudShadowMap, 1024 / 8, 1024 / 8, 1);
                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.BlurCloudShadowMap, TextureIDs._CloudSM, cloudShadowMap);
                        cb.DispatchCompute(cloudCS, (int)CloudPass.BlurCloudShadowMap, 1024 / 8, 1024 / 8, 1);
                        cb.SetGlobalTexture(TextureIDs._CloudShadowMap, cloudShadowMap);



                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.DownSampleDepth, TextureIDs._Depth, depth);
                        downSampledMinMaxDepthDesc.width = depth_wh.x;
                        downSampledMinMaxDepthDesc.height = depth_wh.y;
                        cb.GetTemporaryRT(TextureIDs._DownSampled_MinMax_Depth, downSampledMinMaxDepthDesc, FilterMode.Point);
                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.DownSampleDepth, TextureIDs._DownSampled_MinMax_Depth, TextureIDs._DownSampled_MinMax_Depth);
                        cb.DispatchCompute(cloudCS, (int)CloudPass.DownSampleDepth, dispatch_size_half.x, dispatch_size_half.y, 1);



                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.GetRayIndex, TextureIDs._Depth, TextureIDs._DownSampled_MinMax_Depth);
                        rayIndexDesc.width = ray_wh.x;
                        rayIndexDesc.height = ray_wh.y;
                        cb.GetTemporaryRT(TextureIDs._Ray_Index, rayIndexDesc);
                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.GetRayIndex, TextureIDs._Ray_Index, TextureIDs._Ray_Index);
                        cb.DispatchCompute(cloudCS, (int)CloudPass.GetRayIndex, dispatch_size_quarter.x, dispatch_size_quarter.y, 1);



                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.MarchRay, TextureIDs._Ray_Index, TextureIDs._Ray_Index);
                        rgba16Desc.width = ray_wh.x;
                        rgba16Desc.height = ray_wh.y;
                        cb.GetTemporaryRT(TextureIDs._Marching_Result_A, rgba16Desc);
                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.MarchRay, TextureIDs._Marching_Result_A, TextureIDs._Marching_Result_A);
                        cb.DispatchCompute(cloudCS, (int)CloudPass.MarchRay, dispatch_size_quarter.x, dispatch_size_quarter.y, 1);



                        rgba16Desc.width = depth_wh.x;
                        rgba16Desc.height = depth_wh.y;
                        cb.GetTemporaryRT(TextureIDs._HalfResResult, rgba16Desc);
                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.CheckboardUpsample, TextureIDs._Marching_Result_A, TextureIDs._Marching_Result_A);
                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.CheckboardUpsample, TextureIDs._Ray_Index, TextureIDs._Ray_Index);
                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.CheckboardUpsample, TextureIDs._History, his);
                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.CheckboardUpsample, TextureIDs._DownSampled_MinMax_Depth, TextureIDs._DownSampled_MinMax_Depth);
                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.CheckboardUpsample, TextureIDs._HalfResResult, TextureIDs._HalfResResult);
                        cb.DispatchCompute(cloudCS, (int)CloudPass.CheckboardUpsample, dispatch_size_half.x, dispatch_size_half.y, 1);



                        cb.CopyTexture(TextureIDs._HalfResResult, his);
                        //cb.SetComputeTextureParam(cloudCS, (int)CloudPass.BlitToHistory, TextureIDs._History, TextureIDs._HalfResResult);
                        //cb.SetComputeTextureParam(cloudCS, (int)CloudPass.BlitToHistory, TextureIDs._DownSampled_MinMax_Depth, TextureIDs._DownSampled_MinMax_Depth);
                        //cb.SetComputeTextureParam(cloudCS, (int)CloudPass.BlitToHistory, TextureIDs._HalfResResult, his);
                        //cb.DispatchCompute(cloudCS, (int)CloudPass.BlitToHistory, dispatch_size_half.x, dispatch_size_half.y, 1);


                        cb.CopyTexture(target, tempColor);
                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.FullResolutionUpsample, TextureIDs._Depth, depth);
                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.FullResolutionUpsample, TextureIDs._DownSampled_MinMax_Depth, TextureIDs._DownSampled_MinMax_Depth);
                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.FullResolutionUpsample, TextureIDs._History, his);
                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.FullResolutionUpsample, TextureIDs._SceneColorTex, tempColor);
                        rgba16Desc.width = target_WH.x;
                        rgba16Desc.height = target_WH.y;
                        cb.GetTemporaryRT(TextureIDs._Cloud, rgba16Desc);
                        cb.SetComputeTextureParam(cloudCS, (int)CloudPass.FullResolutionUpsample, TextureIDs._Cloud, TextureIDs._Cloud);
                        cb.DispatchCompute(cloudCS, (int)CloudPass.FullResolutionUpsample, dispatch_size_full.x, dispatch_size_full.y, 1);



                        cb.SetGlobalTexture(TextureIDs._Cloud, TextureIDs._Cloud);
                        cb.CopyTexture(TextureIDs._Cloud, target);


                        cb.ReleaseTemporaryRT(TextureIDs._HalfResResult);
                        cb.ReleaseTemporaryRT(TextureIDs._Marching_Result_A);
                        cb.ReleaseTemporaryRT(TextureIDs._Ray_Index);
                        cb.ReleaseTemporaryRT(TextureIDs._DownSampled_MinMax_Depth);
                        cb.ReleaseTemporaryRT(TextureIDs._Cloud);
                    }
                }

                if (skyBox.connected)
                    atmo.RenderAtmoToCubeMap(cb, skyBox);

                cb.ReleaseTemporaryRT(tempColor);
            }
            else
            {
                if (skyBox.connected)
                    cb.ClearSkybox(skyBox, false, true, Color.clear);

                if (sunBuffer.connected)
                    cb.SetBufferData(sunBuffer, sunLightClear);
            }
            cb.SetGlobalConstantBuffer(sunBuffer, "_Sun", 0, 28);
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