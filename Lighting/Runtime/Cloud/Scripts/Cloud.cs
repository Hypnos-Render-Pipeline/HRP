using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Assertions.Must;
using UnityEngine.Rendering;
using TextureParameter = UnityEngine.Rendering.PostProcessing.TextureParameter;
using FloatParameter = UnityEngine.Rendering.PostProcessing.FloatParameter;
using BoolParameter = UnityEngine.Rendering.PostProcessing.BoolParameter;

[Serializable]
[UnityEngine.Rendering.PostProcessing.PostProcess(typeof(CloudRender), PostProcessEvent.BeforeStack, "Custom/Cloud")]
public sealed class Cloud : PostProcessEffectSettings
{
    public TextureParameter weatherTexture = new TextureParameter { value = null };

    public TextureParameter highCloudTexture = new TextureParameter { value = null };

    public TextureParameter spaceTexture = new TextureParameter { value = null };

    [Range(0, 10)]
    public FloatParameter gapLight = new FloatParameter { value = 1.0f };
    [Range(0.001f, 0.1f)]
    public FloatParameter cloudDensity = new FloatParameter { value = 0.05f };

    [Range(1, 10f)]
    public FloatParameter cloudTexScale = new FloatParameter { value = 1.0f };

    [Range(0f, 4f)]
    public FloatParameter cloudGIIntensity = new FloatParameter { value = 1.0f };

    [Range(0f, 1f)]
    public FloatParameter cloudCoverage = new FloatParameter { value = 1f };

    public BoolParameter enableAurora = new BoolParameter { value = false };

    public BoolParameter workOnSceneView = new BoolParameter { value = true };

}


//#if UNITY_EDITOR
//[PostProcessEditor(typeof(Cloud))]
//internal sealed class CloudOcclusionEditor : PostProcessEffectEditor<Cloud>
//{
//    public override void OnInspectorGUI()
//    {
//        base.OnInspectorGUI();
//        EditorGUILayout.LabelField("When using this effect, turn off \"Atmo\" efect");
//    }
//}
//#endif

public sealed class CloudRender : PostProcessEffectRenderer<Cloud>
{
    ComputeShader cloudShader;
    RenderTextureDescriptor downSampledMinMaxDepthDesc;
    RenderTextureDescriptor rayIndexDesc;
    RenderTextureDescriptor rgba16Desc;
    RenderTextureDescriptor r16Desc;

    Light light;
    CommandBuffer lightCb;
    RenderTexture sm;


    RenderTexture t_table;
    RenderTexture j_table;
    Texture2D _CurlNoise;
    RenderTexture _WorleyPerlinVolume3D, _WorleyVolume3D;

    class PerCameraResources
    {
        Camera cam;
        public int clock;
        public RenderTexture _History;
        public PerCameraResources(Camera camera)
        {
            clock = 0;
            _History = null;
            cam = camera;
        }
        ~PerCameraResources()
        {
            if (_History != null) _History.Release();
        }
        public PerCameraResources ChangeCheck()
        {
            clock++;
            int w = cam.pixelWidth / 2, h = cam.pixelHeight / 2;
            if (_History == null || _History.width != w || _History.height != h)
            {
                if (_History != null) _History.Release();
                _History = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBFloat)
                {
                    enableRandomWrite = true,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                };
                _History.Create();
            }
            return this;
        }
    }

    Dictionary<Camera, PerCameraResources> perCameraResources;


    struct ShaderIDs{
        public enum Pass
        {
            DownSampleDepth = 0,
            GetRayIndex,
            MarchRay,
            CheckboardUpsample,
            CheckboardUpsample_SceneCamera,
            BlitToHistory,
            FullResolutionUpsample,
            LoadVolumeData,
            WriteCloudShadowMap,
        }

        public struct TextureIDs
        {
            public static int _Depth = Shader.PropertyToID(nameof(_Depth));
            public static int _DownSampled_MinMax_Depth = Shader.PropertyToID(nameof(_DownSampled_MinMax_Depth));
            public static int _Ray_Index = Shader.PropertyToID(nameof(_Ray_Index));
            public static int _SceneColor = Shader.PropertyToID(nameof(_SceneColor));
            public static int _Marching_Result_A = Shader.PropertyToID(nameof(_Marching_Result_A));
            public static int _History = Shader.PropertyToID(nameof(_History));
            public static int _HistoryDepth = Shader.PropertyToID(nameof(_HistoryDepth));
            public static int _CameraMotionVectorsTexture = Shader.PropertyToID(nameof(_CameraMotionVectorsTexture));
            public static int _HalfResResult = Shader.PropertyToID(nameof(_HalfResResult));
            public static int _Cloud = Shader.PropertyToID(nameof(_Cloud));
            public static int _ShadowMapTexture = Shader.PropertyToID(nameof(_ShadowMapTexture));
            public static int _WeatherTex = Shader.PropertyToID(nameof(_WeatherTex));
            public static int _CurlNoise = Shader.PropertyToID(nameof(_CurlNoise));
            public static int T_table = Shader.PropertyToID(nameof(T_table));
            public static int J_table = Shader.PropertyToID(nameof(J_table));

            public static int _Volume2D = Shader.PropertyToID(nameof(_Volume2D));
            public static int _Volume3D = Shader.PropertyToID(nameof(_Volume3D));
            public static int _WorleyVolume = Shader.PropertyToID(nameof(_WorleyVolume));
            public static int _WorleyPerlinVolume = Shader.PropertyToID(nameof(_WorleyPerlinVolume));
            public static int _SpaceTexture = Shader.PropertyToID(nameof(_SpaceTexture));
            public static int _HighCloudTexture = Shader.PropertyToID(nameof(_HighCloudTexture));

            public static int _CloudSM = Shader.PropertyToID(nameof(_CloudSM));
            public static int _CloudShadowMap = Shader.PropertyToID(nameof(_CloudShadowMap)); 
        }

        public struct Properties
        {
            public static int _WH = Shader.PropertyToID(nameof(_WH));
            public static int _Clock = Shader.PropertyToID(nameof(_Clock));
            public static int _V = Shader.PropertyToID(nameof(_V));
            public static int _Exp = Shader.PropertyToID(nameof(_Exp));

            public static int _CloudMat = Shader.PropertyToID(nameof(_CloudMat));
            public static int _CloudMat_Inv = Shader.PropertyToID(nameof(_CloudMat_Inv));

            public static int _GapLightIntensity = Shader.PropertyToID(nameof(_GapLightIntensity));
            public static int _CloudCoverage = Shader.PropertyToID(nameof(_CloudCoverage));
            public static int _CloudDensity = Shader.PropertyToID(nameof(_CloudDensity));
            public static int _CloudTexScale = Shader.PropertyToID(nameof(_CloudTexScale)); 
            public static int _EnableAurora = Shader.PropertyToID(nameof(_EnableAurora)); 
            public static int _CloudGIIntensity = Shader.PropertyToID(nameof(_CloudGIIntensity)); 

            public static int _Size_XAtlas_Y_Atlas = Shader.PropertyToID(nameof(_Size_XAtlas_Y_Atlas)); 
        }
    }

    public override void Init()
    {
        base.Init();

        t_table = Resources.Load<CustomRenderTexture>("Shaders/T_Table/T_table");
        j_table = Resources.Load<CustomRenderTexture>("Shaders/Loop/J_table_2");

        _CurlNoise = Resources.Load<Texture2D>("Textures/CurlNoise");
        var lights = GameObject.FindObjectsOfType<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Directional && lights[i].isActiveAndEnabled == true && lights[i].gameObject.activeInHierarchy == true)
            {
                light = lights[i];
                break;
            }
        }
        if (light != null)
        {
            var cbs = light.GetCommandBuffers(LightEvent.AfterShadowMap);
            if (cbs.Length != 0)
            {
                lightCb = cbs[0];
            }
            else
            {
                lightCb = new CommandBuffer();
                lightCb.name = "CopyShadowMap";
                light.AddCommandBuffer(LightEvent.AfterShadowMap, lightCb);
            }
        }
        sm = new RenderTexture(4096, 4096, 0, RenderTextureFormat.RFloat);
 
        cloudShader = Resources.Load<ComputeShader>("Shaders/Cloud");
        perCameraResources = new Dictionary<Camera, PerCameraResources>();
        downSampledMinMaxDepthDesc = new RenderTextureDescriptor();
        downSampledMinMaxDepthDesc.autoGenerateMips = false;
        downSampledMinMaxDepthDesc.colorFormat = RenderTextureFormat.RHalf;
        downSampledMinMaxDepthDesc.enableRandomWrite = true;
        downSampledMinMaxDepthDesc.depthBufferBits = 0;
        downSampledMinMaxDepthDesc.msaaSamples = 1;
        downSampledMinMaxDepthDesc.dimension = TextureDimension.Tex2D;
        rayIndexDesc = downSampledMinMaxDepthDesc;
        rayIndexDesc.colorFormat = RenderTextureFormat.RGFloat;
        rgba16Desc = rayIndexDesc;
        rgba16Desc.colorFormat = RenderTextureFormat.ARGBFloat;

        r16Desc = new RenderTextureDescriptor(1024, 1024, RenderTextureFormat.RHalf);
        r16Desc.enableRandomWrite = true;
        r16Desc.autoGenerateMips = false;
        r16Desc.depthBufferBits = 0;
        r16Desc.msaaSamples = 1;
        r16Desc.dimension = TextureDimension.Tex2D;

        _WorleyVolume3D = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        _WorleyVolume3D.enableRandomWrite = true;
        _WorleyVolume3D.dimension = TextureDimension.Tex3D;
        _WorleyVolume3D.volumeDepth = 32;
        _WorleyVolume3D.wrapMode = TextureWrapMode.Repeat;
        _WorleyVolume3D.filterMode = FilterMode.Trilinear;
        _WorleyVolume3D.Create();
        _WorleyPerlinVolume3D = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        _WorleyPerlinVolume3D.enableRandomWrite = true;
        _WorleyPerlinVolume3D.dimension = TextureDimension.Tex3D;
        _WorleyPerlinVolume3D.volumeDepth = 128;
        _WorleyPerlinVolume3D.wrapMode = TextureWrapMode.Repeat;
        _WorleyPerlinVolume3D.filterMode = FilterMode.Trilinear;
        _WorleyPerlinVolume3D.Create();
        
        var _WorleyPerlinVolume2D = Resources.Load<Texture2D>("Textures/WorleyPerlinVolume");
        var _WorleyVolume2D = Resources.Load<Texture2D>("Textures/WorleyVolume");
        CommandBuffer writeVolumecb = new CommandBuffer();
        writeVolumecb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.LoadVolumeData.ToInt(), ShaderIDs.TextureIDs._Volume2D, _WorleyPerlinVolume2D);
        writeVolumecb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.LoadVolumeData.ToInt(), ShaderIDs.TextureIDs._Volume3D, _WorleyPerlinVolume3D);
        writeVolumecb.SetComputeVectorParam(cloudShader, ShaderIDs.Properties._Size_XAtlas_Y_Atlas, new Vector4(128, 16, 8));
        writeVolumecb.DispatchCompute(cloudShader, ShaderIDs.Pass.LoadVolumeData.ToInt(), 32, 32, 32);
        writeVolumecb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.LoadVolumeData.ToInt(), ShaderIDs.TextureIDs._Volume2D, _WorleyVolume2D);
        writeVolumecb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.LoadVolumeData.ToInt(), ShaderIDs.TextureIDs._Volume3D, _WorleyVolume3D);
        writeVolumecb.SetComputeVectorParam(cloudShader, ShaderIDs.Properties._Size_XAtlas_Y_Atlas, new Vector4(32, 32, 1));
        writeVolumecb.DispatchCompute(cloudShader, ShaderIDs.Pass.LoadVolumeData.ToInt(), 8, 8, 8);
        Graphics.ExecuteCommandBuffer(writeVolumecb);
    }

    public override void Render(PostProcessRenderContext context)
    {
        var cb = context.command;
        var cam = context.camera;
#if UNITY_EDITOR
        if (cam.name == "SceneCamera")
        {
            if (!settings.workOnSceneView)
            {
                cb.Blit(context.source, context.destination);
                return;
            }
        }
#endif
        cam.depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
        if (!perCameraResources.ContainsKey(cam)) perCameraResources[cam] = new PerCameraResources(cam);
        var resources = perCameraResources[cam].ChangeCheck();
        var _Clock = resources.clock;
        var _History = resources._History;

#if UNITY_EDITOR
        // this line is for fvck unity, sometimes, they will release resources but don't recall Init() and start render.
        if (sm == null)
        {
            sm = new RenderTexture(4096, 4096, 0, RenderTextureFormat.RFloat);
            var lights = GameObject.FindObjectsOfType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional && lights[i].isActiveAndEnabled == true && lights[i].gameObject.activeInHierarchy == true)
                {
                    light = lights[i];
                    break;
                }
            }
            if (light != null)
            {
                var cbs = light.GetCommandBuffers(LightEvent.AfterShadowMap);
                if (cbs.Length != 0)
                {
                    lightCb = cbs[0];
                }
                else
                {
                    lightCb = new CommandBuffer();
                    lightCb.name = "CopyShadowMap";
                    light.AddCommandBuffer(LightEvent.AfterShadowMap, lightCb);
                }
            }
            _WorleyVolume3D = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            _WorleyVolume3D.enableRandomWrite = true;
            _WorleyVolume3D.dimension = TextureDimension.Tex3D;
            _WorleyVolume3D.volumeDepth = 32;
            _WorleyVolume3D.wrapMode = TextureWrapMode.Repeat;
            _WorleyVolume3D.filterMode = FilterMode.Trilinear;
            _WorleyVolume3D.Create();
            _WorleyPerlinVolume3D = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            _WorleyPerlinVolume3D.enableRandomWrite = true;
            _WorleyPerlinVolume3D.dimension = TextureDimension.Tex3D;
            _WorleyPerlinVolume3D.volumeDepth = 128;
            _WorleyPerlinVolume3D.wrapMode = TextureWrapMode.Repeat;
            _WorleyPerlinVolume3D.filterMode = FilterMode.Trilinear;
            _WorleyPerlinVolume3D.Create();

            var _WorleyPerlinVolume2D = Resources.Load<Texture2D>("Textures/WorleyPerlinVolume");
            var _WorleyVolume2D = Resources.Load<Texture2D>("Textures/WorleyVolume");
            CommandBuffer writeVolumecb = new CommandBuffer();
            writeVolumecb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.LoadVolumeData.ToInt(), ShaderIDs.TextureIDs._Volume2D, _WorleyPerlinVolume2D);
            writeVolumecb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.LoadVolumeData.ToInt(), ShaderIDs.TextureIDs._Volume3D, _WorleyPerlinVolume3D);
            writeVolumecb.SetComputeVectorParam(cloudShader, ShaderIDs.Properties._Size_XAtlas_Y_Atlas, new Vector4(128, 16, 8));
            writeVolumecb.DispatchCompute(cloudShader, ShaderIDs.Pass.LoadVolumeData.ToInt(), 32, 32, 32);
            writeVolumecb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.LoadVolumeData.ToInt(), ShaderIDs.TextureIDs._Volume2D, _WorleyVolume2D);
            writeVolumecb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.LoadVolumeData.ToInt(), ShaderIDs.TextureIDs._Volume3D, _WorleyVolume3D);
            writeVolumecb.SetComputeVectorParam(cloudShader, ShaderIDs.Properties._Size_XAtlas_Y_Atlas, new Vector4(32, 32, 1));
            writeVolumecb.DispatchCompute(cloudShader, ShaderIDs.Pass.LoadVolumeData.ToInt(), 8, 8, 8);
            Graphics.ExecuteCommandBuffer(writeVolumecb);
        }
#endif

        RenderTargetIdentifier shadowmap = BuiltinRenderTextureType.CurrentActive;
        lightCb.Clear();
        lightCb.SetShadowSamplingMode(shadowmap, ShadowSamplingMode.RawDepth);
        lightCb.Blit(shadowmap, sm);

        cb.SetGlobalTexture(ShaderIDs.TextureIDs._ShadowMapTexture, sm);
        if (settings.weatherTexture.value != null)
            cb.SetGlobalTexture(ShaderIDs.TextureIDs._WeatherTex, settings.weatherTexture.value);
        else
            cb.SetGlobalTexture(ShaderIDs.TextureIDs._WeatherTex, Texture2D.blackTexture);
        cb.SetGlobalTexture(ShaderIDs.TextureIDs._CurlNoise, _CurlNoise);
        cb.SetGlobalTexture(ShaderIDs.TextureIDs._WorleyVolume, _WorleyVolume3D);
        cb.SetGlobalTexture(ShaderIDs.TextureIDs._WorleyPerlinVolume, _WorleyPerlinVolume3D);
        if (settings.spaceTexture.value != null)
            cb.SetGlobalTexture(ShaderIDs.TextureIDs._SpaceTexture, settings.spaceTexture.value);
        else
            cb.SetGlobalTexture(ShaderIDs.TextureIDs._SpaceTexture, Texture2D.blackTexture);

        if (settings.highCloudTexture.value != null)
            cb.SetGlobalTexture(ShaderIDs.TextureIDs._HighCloudTexture, settings.highCloudTexture.value);
        else
            cb.SetGlobalTexture(ShaderIDs.TextureIDs._HighCloudTexture, Texture2D.blackTexture);

        cb.SetGlobalMatrix(ShaderIDs.Properties._V, cam.worldToCameraMatrix);

        cb.SetGlobalTexture(ShaderIDs.TextureIDs.T_table, t_table);
        cb.SetGlobalTexture(ShaderIDs.TextureIDs.J_table, j_table);
        cb.SetGlobalFloat(ShaderIDs.Properties._Exp, 4.4f);
        cb.SetGlobalFloat(ShaderIDs.Properties._GapLightIntensity, settings.gapLight);
        cb.SetGlobalFloat(ShaderIDs.Properties._CloudCoverage, settings.cloudCoverage);
        cb.SetGlobalFloat(ShaderIDs.Properties._CloudDensity, settings.cloudDensity);
        cb.SetGlobalFloat(ShaderIDs.Properties._CloudTexScale, settings.cloudTexScale);
        cb.SetGlobalInt(ShaderIDs.Properties._EnableAurora, settings.enableAurora ? 1 : 0);
        cb.SetGlobalFloat(ShaderIDs.Properties._CloudGIIntensity, settings.cloudGIIntensity);
        
        {

            Vector4 _WH = new Vector4(cam.pixelWidth, cam.pixelHeight, 1.0f / cam.pixelWidth, 1.0f / cam.pixelHeight);
            Vector2Int depth_wh = new Vector2Int((int)(_WH.x / 2), (int)(_WH.y / 2));
            Vector2Int ray_wh = new Vector2Int(depth_wh.x / 2, depth_wh.y / 2);

            Vector2Int dispatch_size_full = new Vector2Int(cam.pixelWidth / 8 + (cam.pixelWidth % 8 != 0 ? 1 : 0), cam.pixelHeight / 8 + (cam.pixelHeight % 8 != 0 ? 1 : 0));
            Vector2Int dispatch_size_half = new Vector2Int(depth_wh.x / 8 + (depth_wh.x % 8 != 0 ? 1 : 0), depth_wh.y / 8 + (depth_wh.y % 8 != 0 ? 1 : 0));
            Vector2Int dispatch_size_quarter = new Vector2Int(ray_wh.x / 8 + (ray_wh.x % 8 != 0 ? 1 : 0), ray_wh.y / 8 + (ray_wh.y % 8 != 0 ? 1 : 0));

            cb.SetGlobalVector(ShaderIDs.Properties._WH, _WH);
            cb.SetGlobalInt(ShaderIDs.Properties._Clock, _Clock);

            var cloudTrans = ConstructCloudTransform(cam, light);
            
            cb.SetGlobalMatrix(ShaderIDs.Properties._CloudMat, cloudTrans);
            cb.SetGlobalMatrix(ShaderIDs.Properties._CloudMat_Inv, cloudTrans.inverse);
            


            cb.GetTemporaryRT(ShaderIDs.TextureIDs._CloudSM, r16Desc, FilterMode.Bilinear);
            cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.WriteCloudShadowMap.ToInt(), ShaderIDs.TextureIDs._CloudSM, ShaderIDs.TextureIDs._CloudSM);

            cb.DispatchCompute(cloudShader, ShaderIDs.Pass.WriteCloudShadowMap.ToInt(), 1024 / 8, 1024 / 8, 1);

            cb.SetGlobalTexture(ShaderIDs.TextureIDs._CloudShadowMap, ShaderIDs.TextureIDs._CloudSM);



            cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.DownSampleDepth.ToInt(), ShaderIDs.TextureIDs._Depth, BuiltinRenderTextureType.ResolvedDepth);

            downSampledMinMaxDepthDesc.width = depth_wh.x;
            downSampledMinMaxDepthDesc.height = depth_wh.y;
            cb.GetTemporaryRT(ShaderIDs.TextureIDs._DownSampled_MinMax_Depth, downSampledMinMaxDepthDesc, FilterMode.Point);
            cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.DownSampleDepth.ToInt(), ShaderIDs.TextureIDs._DownSampled_MinMax_Depth, ShaderIDs.TextureIDs._DownSampled_MinMax_Depth);

            cb.DispatchCompute(cloudShader, ShaderIDs.Pass.DownSampleDepth.ToInt(), dispatch_size_half.x, dispatch_size_half.y, 1);



            cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.GetRayIndex.ToInt(), ShaderIDs.TextureIDs._Depth, ShaderIDs.TextureIDs._DownSampled_MinMax_Depth);

            rayIndexDesc.width = ray_wh.x;
            rayIndexDesc.height = ray_wh.y;
            cb.GetTemporaryRT(ShaderIDs.TextureIDs._Ray_Index, rayIndexDesc);
            cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.GetRayIndex.ToInt(), ShaderIDs.TextureIDs._Ray_Index, ShaderIDs.TextureIDs._Ray_Index);

            cb.DispatchCompute(cloudShader, ShaderIDs.Pass.GetRayIndex.ToInt(), dispatch_size_quarter.x, dispatch_size_quarter.y, 1);



            cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.MarchRay.ToInt(), ShaderIDs.TextureIDs._Ray_Index, ShaderIDs.TextureIDs._Ray_Index);

            rgba16Desc.width = ray_wh.x;
            rgba16Desc.height = ray_wh.y;
            cb.GetTemporaryRT(ShaderIDs.TextureIDs._Marching_Result_A, rgba16Desc);
            cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.MarchRay.ToInt(), ShaderIDs.TextureIDs._Marching_Result_A, ShaderIDs.TextureIDs._Marching_Result_A);

            cb.BeginSample("Marching");
            cb.DispatchCompute(cloudShader, ShaderIDs.Pass.MarchRay.ToInt(), dispatch_size_quarter.x, dispatch_size_quarter.y, 1);
            cb.EndSample("Marching");



            rgba16Desc.width = depth_wh.x;
            rgba16Desc.height = depth_wh.y;
            cb.GetTemporaryRT(ShaderIDs.TextureIDs._HalfResResult, rgba16Desc);
#if UNITY_EDITOR
            if (cam.name != "SceneCamera")
            {
#endif
                cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.CheckboardUpsample.ToInt(), ShaderIDs.TextureIDs._Marching_Result_A, ShaderIDs.TextureIDs._Marching_Result_A);
                cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.CheckboardUpsample.ToInt(), ShaderIDs.TextureIDs._Ray_Index, ShaderIDs.TextureIDs._Ray_Index);
                cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.CheckboardUpsample.ToInt(), ShaderIDs.TextureIDs._History, _History);
                cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.CheckboardUpsample.ToInt(), ShaderIDs.TextureIDs._DownSampled_MinMax_Depth, ShaderIDs.TextureIDs._DownSampled_MinMax_Depth);
                cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.CheckboardUpsample.ToInt(), ShaderIDs.TextureIDs._CameraMotionVectorsTexture, BuiltinRenderTextureType.MotionVectors);

                cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.CheckboardUpsample.ToInt(), ShaderIDs.TextureIDs._HalfResResult, ShaderIDs.TextureIDs._HalfResResult);

                cb.DispatchCompute(cloudShader, ShaderIDs.Pass.CheckboardUpsample.ToInt(), dispatch_size_half.x, dispatch_size_half.y, 1);
#if UNITY_EDITOR
            }
            else // because the motion texture of scene camera is wrong and we can't fix it in buildin pipline.
            {
                cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.CheckboardUpsample_SceneCamera.ToInt(), ShaderIDs.TextureIDs._Marching_Result_A, ShaderIDs.TextureIDs._Marching_Result_A);
                cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.CheckboardUpsample_SceneCamera.ToInt(), ShaderIDs.TextureIDs._Ray_Index, ShaderIDs.TextureIDs._Ray_Index);
                cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.CheckboardUpsample_SceneCamera.ToInt(), ShaderIDs.TextureIDs._History, _History);
                cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.CheckboardUpsample_SceneCamera.ToInt(), ShaderIDs.TextureIDs._DownSampled_MinMax_Depth, ShaderIDs.TextureIDs._DownSampled_MinMax_Depth);
                cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.CheckboardUpsample_SceneCamera.ToInt(), ShaderIDs.TextureIDs._CameraMotionVectorsTexture, BuiltinRenderTextureType.MotionVectors);

                cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.CheckboardUpsample_SceneCamera.ToInt(), ShaderIDs.TextureIDs._HalfResResult, ShaderIDs.TextureIDs._HalfResResult);

                cb.DispatchCompute(cloudShader, ShaderIDs.Pass.CheckboardUpsample_SceneCamera.ToInt(), dispatch_size_half.x, dispatch_size_half.y, 1);
            }
#endif



            // this not a bug, because '_HalfResResult' slot is RW, so bind them like this.
            cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.BlitToHistory.ToInt(), ShaderIDs.TextureIDs._History, ShaderIDs.TextureIDs._HalfResResult);
            cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.BlitToHistory.ToInt(), ShaderIDs.TextureIDs._DownSampled_MinMax_Depth, ShaderIDs.TextureIDs._DownSampled_MinMax_Depth);

            cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.BlitToHistory.ToInt(), ShaderIDs.TextureIDs._HalfResResult, _History);

            cb.DispatchCompute(cloudShader, ShaderIDs.Pass.BlitToHistory.ToInt(), dispatch_size_half.x, dispatch_size_half.y, 1);



            cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.FullResolutionUpsample.ToInt(), ShaderIDs.TextureIDs._Depth, BuiltinRenderTextureType.ResolvedDepth);
            cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.FullResolutionUpsample.ToInt(), ShaderIDs.TextureIDs._DownSampled_MinMax_Depth, ShaderIDs.TextureIDs._DownSampled_MinMax_Depth);
            cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.FullResolutionUpsample.ToInt(), ShaderIDs.TextureIDs._History, _History);

            rgba16Desc.width = cam.pixelWidth;
            rgba16Desc.height = cam.pixelHeight;
            cb.GetTemporaryRT(ShaderIDs.TextureIDs._Cloud, rgba16Desc, FilterMode.Point);
            cb.SetComputeTextureParam(cloudShader, ShaderIDs.Pass.FullResolutionUpsample.ToInt(), ShaderIDs.TextureIDs._Cloud, ShaderIDs.TextureIDs._Cloud);

            cb.DispatchCompute(cloudShader, ShaderIDs.Pass.FullResolutionUpsample.ToInt(), dispatch_size_full.x, dispatch_size_full.y, 1);



            cb.SetGlobalTexture(ShaderIDs.TextureIDs._Cloud, ShaderIDs.TextureIDs._Cloud);

            var sheet = context.propertySheets.Get(Shader.Find("Hidden/Custom/CloudMix"));
            cb.BeginSample("Mix");
            cb.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
            cb.EndSample("Mix");



            cb.ReleaseTemporaryRT(ShaderIDs.TextureIDs._HalfResResult);
            cb.ReleaseTemporaryRT(ShaderIDs.TextureIDs._Marching_Result_A);
            cb.ReleaseTemporaryRT(ShaderIDs.TextureIDs._Ray_Index);
            cb.ReleaseTemporaryRT(ShaderIDs.TextureIDs._DownSampled_MinMax_Depth);
        }
    }

    static Matrix4x4 ConstructCloudTransform(Camera cam, Light light)
    {
        float planet_radius = 6371e3f;
        Vector2 cloud_radi = new Vector2(800, 2400);
        Vector3 acture_x = cam.transform.position;
        Vector3 x = acture_x;
        x.y = Mathf.Max(x.y, 95) + planet_radius;
        Vector3 s = -light.transform.forward;
        s = s.normalized;
        s = s.y < -0.05 ? -s : s;
        float depth = Mathf.Min(2048, cam.farClipPlane);
        
        var p0 = cam.ScreenToWorldPoint(new Vector3(0, 0, depth), Camera.MonoOrStereoscopicEye.Mono) - acture_x + x;
        var p1 = cam.ScreenToWorldPoint(new Vector3(cam.pixelWidth, 0, depth), Camera.MonoOrStereoscopicEye.Mono) - acture_x + x;
        var p2 = cam.ScreenToWorldPoint(new Vector3(cam.pixelWidth, cam.pixelHeight, depth), Camera.MonoOrStereoscopicEye.Mono) - acture_x + x;
        var p3 = cam.ScreenToWorldPoint(new Vector3(0, cam.pixelHeight, depth), Camera.MonoOrStereoscopicEye.Mono) - acture_x + x;
        var p4 = x;
        //Debug.DrawLine(p4 - x + acture_x, p0 - x + acture_x);
        //Debug.DrawLine(p4 - x + acture_x, p1 - x + acture_x);
        //Debug.DrawLine(p4 - x + acture_x, p2 - x + acture_x);
        //Debug.DrawLine(p4 - x + acture_x, p3 - x + acture_x);
        //Debug.DrawLine(p0 - x + acture_x, p1 - x + acture_x);
        //Debug.DrawLine(p1 - x + acture_x, p2 - x + acture_x);
        //Debug.DrawLine(p2 - x + acture_x, p3 - x + acture_x);
        //Debug.DrawLine(p3 - x + acture_x, p0 - x + acture_x);

        float k;
        IntersectSphere(p0, s, Vector3.zero, planet_radius + cloud_radi.x, out k);
        p0 += s * k;
        IntersectSphere(p1, s, Vector3.zero, planet_radius + cloud_radi.x, out k);
        p1 += s * k;
        IntersectSphere(p2, s, Vector3.zero, planet_radius + cloud_radi.x, out k);
        p2 += s * k;
        IntersectSphere(p3, s, Vector3.zero, planet_radius + cloud_radi.x, out k);
        p3 += s * k;
        IntersectSphere(p4, s, Vector3.zero, planet_radius + cloud_radi.x, out k);
        p4 += s * k;
        
        Matrix4x4 lightMatrix = light.transform.localToWorldMatrix;
        p0 = lightMatrix.inverse.MultiplyPoint(p0);
        p1 = lightMatrix.inverse.MultiplyPoint(p1);
        p2 = lightMatrix.inverse.MultiplyPoint(p2);
        p3 = lightMatrix.inverse.MultiplyPoint(p3);
        p4 = lightMatrix.inverse.MultiplyPoint(p4);

        var min_vec = Vector3.Min(p0, Vector3.Min(p1, Vector3.Min(p2, Vector3.Min(p3, p4)))) - Vector3.one * 10;
        var max_vec = Vector3.Max(p0, Vector3.Max(p1, Vector3.Max(p2, Vector3.Max(p3, p4)))) + Vector3.one * 10;
        //DrawBB(lightMatrix, acture_x - x, min_vec, max_vec, Color.yellow);
        
        Vector3 frac_vec = lightMatrix.inverse.MultiplyPoint(Vector3.zero);

        frac_vec.x = frac_vec.x - Mathf.Floor(frac_vec.x);
        frac_vec.y = frac_vec.y - Mathf.Floor(frac_vec.y);
        frac_vec.z = 0;
        lightMatrix = lightMatrix * Matrix4x4.Translate(frac_vec);
        
        var scale_vec = max_vec - min_vec;
        float scale = Mathf.Max(scale_vec.x, scale_vec.y);
        lightMatrix = lightMatrix * Matrix4x4.Translate(min_vec) * Matrix4x4.Scale(new Vector3(scale, scale, 1));
        //if (cam.name != "SceneCamera")
        //    DrawTrans(lightMatrix, acture_x - x);

        return lightMatrix.inverse;
    }

#if UNITY_EDITOR
    static void DrawTrans(Matrix4x4 mat, Vector3 offst)
    {
        Vector3 o = mat * new Vector4(0, 0, 0, 1);
        Vector3 x = mat * new Vector4(1, 0, 0, 0);
        Vector3 y = mat * new Vector4(0, 1, 0, 0);
        Vector3 z = mat * new Vector4(0, 0, 1, 0);
        Debug.DrawRay(o + offst, x, Color.red);
        Debug.DrawRay(o + offst, y, Color.green);
        Debug.DrawRay(o + offst, z, Color.blue);
    }
    public static void DrawBB(Matrix4x4 mat, Vector4 offset, Vector3 bbmin, Vector3 bbmax, Color color, float time = 10)
    {
        Vector4 a = bbmin, b = new Vector4(bbmin.x, bbmin.y, bbmax.z),
                c = new Vector4(bbmin.x, bbmax.y, bbmax.z), d = new Vector4(bbmin.x, bbmax.y, bbmin.z);
        Vector4 e = new Vector4(bbmax.x, bbmin.y, bbmin.z), f = new Vector4(bbmax.x, bbmin.y, bbmax.z),
                g = bbmax, h = new Vector4(bbmax.x, bbmax.y, bbmin.z);
        a.w = 1; b.w = 1; c.w = 1; d.w = 1; e.w = 1; f.w = 1; g.w = 1; h.w = 1;
        Draw8Points(mat * a + offset, mat * b + offset, mat * c + offset, mat * d + offset, 
                mat * e + offset, mat * f + offset, mat * g + offset, mat * h + offset,
                color, time);
    }
    public static void Draw8Points(Vector4 a, Vector4 b, Vector4 c, Vector4 d,
                                Vector4 e, Vector4 f, Vector4 g, Vector4 h,
                                Color color, float time = 10)
    {
        Draw4Points(a, b, c, d, color, time);
        Draw4Points(e, f, g, h, color, time);
        Draw4Points(a, b, f, e, color, time);
        Draw4Points(b, c, g, f, color, time);
        Draw4Points(c, d, h, g, color, time);
        Draw4Points(d, a, e, h, color, time);
    }
    public static void Draw4Points(Vector4 a, Vector4 b, Vector4 c, Vector4 d, Color color, float time = 10)
    {
        Debug.DrawLine(a, b, color);
        Debug.DrawLine(b, c, color);
        Debug.DrawLine(c, d, color);
        Debug.DrawLine(d, a, color);
    }
#endif

    static bool IntersectSphere(Vector3 p, Vector3 v, Vector3 o, float r, out float k)
    {
        k = 0;
        Vector3 oc = p - o;
        float b = 2.0f * Vector3.Dot(v, oc);
        float c = Vector3.Dot(oc, oc) - r * r;
        float disc = b * b - 4.0f * c;
        if (disc < 0.0f)
            return false;
        float q = (-b + ((b < 0.0f) ? - Mathf.Sqrt(disc) : Mathf.Sqrt(disc))) / 2.0f;
        float t0 = q;
        float t1 = c / q;
        if (t0 > t1)
        {
            float temp = t0;
            t0 = t1;
            t1 = temp;
        }
        if (t0 > 0.0f) k = t0;
        else k = t1;
        return true;
    }

}
public static class ExtendEnum
{
    public static int ToInt(this Enum e)
    {
        return e.GetHashCode();
    }
}