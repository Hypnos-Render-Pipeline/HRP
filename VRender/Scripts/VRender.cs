using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using static Unity.Mathematics.math;
using UnityEngine.Rendering;
using Unity.Mathematics;
using System.Collections.Generic;
using UnityEditor;
using HypnosRenderPipeline;
using UnityEditor.PackageManager.UI;

[System.Serializable]
public class VRenderParameters
{

    [HideInInspector]
    public bool halfResolution = false;
    [HideInInspector]
    public bool upsample = false;
    //[HideInInspector]

    public LayerMask cullingMask = -1;

    public string noGITag = "";

    public float nearPlane = 0.0f;
    public bool preferFrameRate = false;
    [Min(1)]
    public int temporalFrameNum = 8;
    [Range(1, 4)]
    public int sqrtSamplePerPixel = 1;
    [Range(1, 16)]
    public int maxDepth = 4;

    [Tooltip("Recommend to use it when render indoor scene.")]
    public bool cacheIrradiance = false;

    public float IrradianceVolumeScale = 1;

    public bool enableFog = false;

    [System.Serializable]
    public struct DOF
    {
        [Min(0)]
        public float FocusDistance;
        [Range(0, 10)]
        public float Aperture;
        public Texture2D bokehTexture;
    }

    public DOF DOFConfig = new DOF { FocusDistance = 10f, Aperture = 0f, bokehTexture = null };

    [System.Serializable]
    public struct LightSetting
    {
        [Tooltip("Disable this to Use physical attenuation.")]
        public bool useAttenuationCurve;
        public AnimationCurve attenuationCurve;
        [HideInInspector]
        Keyframe[] frames;
        bool lastUse;
        Texture2D attTex;

        public void Set(LightSetting setting)
        {
            useAttenuationCurve = setting.useAttenuationCurve;
            attenuationCurve = setting.attenuationCurve;
        }

        public bool Refresh(CommandBuffer cb)
        {
            bool changeUse = lastUse != useAttenuationCurve;
            lastUse = useAttenuationCurve;

            cb.SetGlobalTexture("_AttenuationTex", attTex);
            if (!useAttenuationCurve)
            {
                cb.SetGlobalInt("_UseAttenuationCureve", 0);
                return changeUse;
            }
            cb.SetGlobalInt("_UseAttenuationCureve", 1);

            bool dirty = false;
            if (frames.Length != attenuationCurve.keys.Length)
            {
                dirty = true;
            }
            else
            {
                for (int i = 0; i < frames.Length; i++)
                {
                    var a = frames[i];
                    var b = attenuationCurve.keys[i];
                    if (a.time != b.time || a.outTangent != b.outTangent || a.inTangent != b.inTangent || a.inWeight != b.inWeight || a.outWeight != b.outWeight || a.value != b.value)
                    {
                        dirty = true;
                        break;
                    }
                }
            }
            if (dirty)
            {
                frames = attenuationCurve.keys;
                Color[] colors = new Color[256];
                for (int i = 0; i < 256; i++)
                {
                    colors[i] = new Color(attenuationCurve.Evaluate(i / 255.0f), 0, 0);
                }
                attTex.SetPixels(colors);
                attTex.Apply();
            }
            return dirty || changeUse;
        }
        public void Init(bool useCurve = false) {
            frames = new Keyframe[1];
            attTex = new Texture2D(256, 1, TextureFormat.R16, false, true);
            attTex.wrapMode = TextureWrapMode.Clamp;
            if (attenuationCurve == null) attenuationCurve = AnimationCurve.Linear(0, 1, 1, 0);
        }
    }

    public LightSetting lightSetting;


    [System.Serializable]
    public enum DebugMode { none, varience, /*albedo, normal,*/ irradianceCache, /*allInDock */};

    [Space(10.0f)]

    public DebugMode debugMode = DebugMode.none;

    [System.Serializable]
    public enum DenoiseMode { None, QuikDenoise, HisogramDenoise };
    [Space(15)]
    [Header("Quick deniose")]
    public DenoiseMode denosieMode = DenoiseMode.None;
    [Range(0.01f, 1)]
    public float strength = 0.1f;
    [Range(1, 100)]
    public float removeFlare = 10;
}


public class VRender : IDisposable
{
    public VRenderParameters parameters;

    BNSLoader bnsLoader;


    RayTracingShader rtShader_fog, rtShader_nofog;

    RayTracingShader rtShader {
        get 
        {
            if (parameters.enableFog) return rtShader_fog;
            else return rtShader_nofog;
        } 
    }
    Material blitMat;
    Material denoiseMaterial;
    Texture2D ssLut;

    public Camera cam;

    CommandBuffer cb;
    ComputeBuffer cb_LightList;

    RenderTexture frameSamples;

    ComputeBuffer sampleHistogram;

    ComputeShader cs_clearVolume;
    ComputeShader cs_histoDenoise;
    RenderTexture tex3D_iv;

    RenderTexture history = null, record = null;
    RenderTexture denoised = null;

    RenderTexture TLut;
    RenderTexture MSLut;
    RenderTexture VolumeLut;
    RenderTexture SkyLut;

    Texture2D bokehTex;

    int hash;

    public VRender(Camera cam, VRenderParameters parameters = null)
    {
        cb = GetCB(cam, "RayTrace", CameraEvent.BeforeImageEffects);
        cam.depthTextureMode |= DepthTextureMode.DepthNormals | DepthTextureMode.Depth;

        rtShader_fog = Resources.Load<RayTracingShader>("Camera_Fog");
        rtShader_nofog = Resources.Load<RayTracingShader>("Camera");
        blitMat = new Material(Shader.Find("Hidden/VRenderBlit"));
        denoiseMaterial = new Material(Shader.Find("Hidden/QuickDenoise"));
        ssLut = Resources.Load<Texture2D>("Textures/Random Lut/RdLut");
        bnsLoader = HypnosRenderPipeline.BNSLoader.instance;
        cb_LightList = new ComputeBuffer(100, HypnosRenderPipeline.LightListGenerator.lightStructSize);
        cs_clearVolume = Resources.Load<ComputeShader>("ClearVolume");
        cs_histoDenoise = Resources.Load<ComputeShader>("HistogramDenoise");
        tex3D_iv = new RenderTexture(128, 64, 0);
        tex3D_iv.dimension = TextureDimension.Tex3D;
        tex3D_iv.volumeDepth = 128;
        tex3D_iv.format = RenderTextureFormat.ARGBHalf;
        tex3D_iv.autoGenerateMips = false;
        tex3D_iv.enableRandomWrite = true;
        tex3D_iv.Create();

        TLut = new RenderTexture(512, 513, 0, RenderTextureFormat.ARGBFloat);
        MSLut = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGBFloat);
        SkyLut = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGBFloat);
        VolumeLut = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat);
        VolumeLut.enableRandomWrite = true;
        VolumeLut.dimension = TextureDimension.Tex3D;
        VolumeLut.volumeDepth = 1;
        VolumeLut.Create();

        hash = Time.time.GetHashCode();

        bokehTex = Resources.Load<Texture2D>("Bokeh");
        //if (LightManager.sunLight != null && LightManager.sunLight.atmoPreset != null)
        //{
        //    Color lum;
        //    Vector3 dir;
        //    var sun = LightManager.sunLight;
        //    var atmo = sun.atmoPreset;
        //    atmo = sun.atmoPreset;
        //    lum = sun.color * sun.radiance * math.pow(10, 4.6f);
        //    dir = -sun.direction;
        //    atmo.GenerateLut(hash, cb, TLut, MSLut, lum, dir, true);
        //}

        if (parameters == null)
        {
            this.parameters = new VRenderParameters();
        }
        else
        {
            this.parameters = parameters;
        }
        this.parameters.lightSetting.Init();
    }

    ~VRender()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (cb != null)
        {
            cam.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, cb);
            cb.Dispose();
            cb = null;
        }
        if (bnsLoader != null)
        {
            bnsLoader.Dispose();
            bnsLoader = null;
        }
        if (cb_LightList != null)
        {
            cb_LightList.Dispose();
            cb_LightList = null;
        }
        if (tex3D_iv != null)
        {
            tex3D_iv.Release();
            tex3D_iv = null;
        }
        if (history != null)
        {
            history.Release();
            history = null;
        }
        if (record != null)
        {
            record.Release();
            record = null;
        }
        if (denoised != null)
        {
            denoised.Release();
            denoised = null;
        }
        if (sampleHistogram != null)
        {
            sampleHistogram.Release();
            sampleHistogram = null;
        }
        defaultBuffer.Dispose();
        defaultArray.Release();
    }

    int FrameIndex = 0;
    int subFrameIndex = 0;
    int temporalFrameNumSwith = 0;

    public void ClearCB()
    {
        if (cb != null) cb.Clear();
    }

    public void Render(MonoBehaviour host = null)
    {
        SetPerObjectData();

        cb.Clear();

        ParameterChanged();

        if (HypnosRenderPipeline.RTRegister.GetChanged(cam)) ReRender(); 
        if (parameters.lightSetting.Refresh(cb)) ReRender();

        HypnosRenderPipeline.RTRegister.UpdateLightBuffer(cb, cb_LightList);
        UpdateAccelerationStructure();
        UpdateCamera();
        UpdateSkyBox();
        UpdateFog();

        int tmp = parameters.temporalFrameNum;
        parameters.temporalFrameNum = temporalFrameNumSwith;
        temporalFrameNumSwith = tmp;

        int2 resolution = (int2)(float2(cam.pixelWidth, cam.pixelHeight) * (parameters.halfResolution ? 0.5f : 1));

        if (history == null || history.width != resolution.x || history.height != resolution.y)
        {
            if (history != null) history.Release();
            history = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.ARGBFloat);
            history.enableRandomWrite = true;
            history.Create();
        }
        if (record == null || record.width != resolution.x || record.height != resolution.y)
        {
            if (record != null) record.Release();
            record = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.ARGBFloat);
            record.enableRandomWrite = true;
            record.Create();
        }
        SetupFrameSamplesBuffer(host, cb, resolution.x, resolution.y);        
        if (FrameIndex == 1)
        {
            if (parameters.cacheIrradiance)
            {
                cb.SetComputeTextureParam(cs_clearVolume, 0, "_Volume", tex3D_iv);
                cb.DispatchCompute(cs_clearVolume, 0, 128 / 4, 64 / 4, 128 / 4);
            }
            cb.SetComputeBufferParam(cs_histoDenoise, 0, "_HistogramBuffer", sampleHistogram);
            int total_pixel_count = resolution.x * resolution.y;
            cb.DispatchCompute(cs_histoDenoise, 0, (total_pixel_count / 32) + (total_pixel_count % 32 != 0 ? 1 : 0), 1, 1);
        }

        cb.SetGlobalTexture("_Sobol", bnsLoader.tex_sobol);
        cb.SetGlobalTexture("_ScramblingTile", bnsLoader.tex_scrambling);
        cb.SetGlobalTexture("_RankingTile", bnsLoader.tex_rankingTile);
        cb.SetGlobalTexture("_RdLut", ssLut);
        cb.SetGlobalTexture("_Response", parameters.DOFConfig.bokehTexture != null ? parameters.DOFConfig.bokehTexture  : bokehTex);
        cb.SetRayTracingIntParam(rtShader, "_CacheIrradiance", parameters.cacheIrradiance ? (parameters.debugMode == VRenderParameters.DebugMode.irradianceCache ? 2 : 1) : 0);
        cb.SetRayTracingVectorParam(rtShader, "_IVScale", Vector4.one * parameters.IrradianceVolumeScale);
        cb.SetRayTracingTextureParam(rtShader, "_IrrVolume", tex3D_iv);
        cb.SetRayTracingTextureParam(rtShader, "Target", frameSamples);
        cb.SetRayTracingTextureParam(rtShader, "History", history);
        cb.SetRayTracingTextureParam(rtShader, "Record", record);
        cb.SetRayTracingIntParam(rtShader, "_Frame_Index", FrameIndex++);
        cb.SetRayTracingIntParam(rtShader, "_SubFrameIndex", subFrameIndex++);
        cb.SetRayTracingIntParam(rtShader, "_Max_Frame", parameters.temporalFrameNum);
        cb.SetRayTracingIntParam(rtShader, "_sqrt_spp", parameters.sqrtSamplePerPixel);
        cb.SetRayTracingIntParam(rtShader, "_Max_depth", parameters.maxDepth);
        cb.SetRayTracingVectorParam(rtShader, "_DOF", float4(parameters.DOFConfig.FocusDistance, parameters.DOFConfig.Aperture, 0, 0));
        cb.SetRayTracingShaderPass(rtShader, "RT");
        cb.SetRayTracingIntParam(rtShader, "_PreferFrameRate", parameters.preferFrameRate ? 1 : 0);
        cb.SetRayTracingFloatParam(rtShader, "_NearPlane", parameters.nearPlane);
        if (parameters.preferFrameRate)
            cb.DispatchRays(rtShader, "RayGeneration", (uint)resolution.x / 2, (uint)resolution.y / 2, 1, cam);
        else
            cb.DispatchRays(rtShader, "RayGeneration", (uint)resolution.x, (uint)resolution.y / 2, 1, cam);

        cb.SetComputeTextureParam(cs_histoDenoise, 1, "_FrameSamples", frameSamples);
        cb.SetComputeBufferParam(cs_histoDenoise, 1, "_HistogramBuffer", sampleHistogram);
        cb.DispatchCompute(cs_histoDenoise, 1, (resolution.x / 8) + (resolution.x % 8 != 0 ? 1 : 0), (resolution.y / 8) + (resolution.y % 8 != 0 ? 1 : 0), 1);

        switch (parameters.debugMode)
        {
            case VRenderParameters.DebugMode.none:
            case VRenderParameters.DebugMode.irradianceCache:
                BlitResultToScreen(history);
                break;
            case VRenderParameters.DebugMode.varience:
                cb.Blit(record, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget), blitMat, 0);
                break;
            //case VRenderParameters.DebugMode.albedo:
            //    cb.Blit(new RenderTargetIdentifier(BuiltinRenderTextureType.GBuffer0), new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget));
            //    break;
            //case VRenderParameters.DebugMode.normal:
            //    cb.Blit(null, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget), blitMat, 1);
            //    break;
            //case VRenderParameters.DebugMode.allInDock:
            //    BlitResultToScreen(history);
            //    cb.Blit(record, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget), blitMat, 2);
            //    cb.Blit(new RenderTargetIdentifier(BuiltinRenderTextureType.GBuffer0), albedo);
            //    cb.Blit(albedo, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget), blitMat, 3);
            //    cb.Blit(null, normal, blitMat, 1);
            //    cb.Blit(normal, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget), blitMat, 4);
            //    break;
            default:
                break;
        }

        parameters.temporalFrameNum = temporalFrameNumSwith;
    }


    int rendererNum = 0;
    void SetPerObjectData()
    {
        var objs = GameObject.FindObjectsOfType<MeshRenderer>();
        if (rendererNum != objs.Length)
        {
            rendererNum = objs.Length;
            ReRender();
        }
    }

    CommandBuffer GetCB(Camera cam, string name, CameraEvent ce)
    {
        this.cam = cam;
        CommandBuffer res;
        var cbs = cam.GetCommandBuffers(ce);
        if (cbs.Length == 0)
        {
            res = new CommandBuffer();
            res.name = name;
            cam.AddCommandBuffer(ce, res);
        }
        else
        {
            res = cbs[0];
        }
        if (res.name != name)
        {
            var tcb = res;
            res = new CommandBuffer();
            res.name = name;
            cam.RemoveCommandBuffer(ce, tcb);
            cam.AddCommandBuffer(ce, res);
            cam.AddCommandBuffer(ce, tcb);
        }
        return res;
    }



    void BlitResultToScreen(RenderTexture res)
    {
        switch (parameters.denosieMode)
        {
            case VRenderParameters.DenoiseMode.None:
                if (parameters.halfResolution)
                {
                    if (parameters.upsample)
                        cb.Blit(res, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget), denoiseMaterial, 1);
                    else
                        cb.Blit(res, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget));
                }
                    
                else
                    cb.Blit(res, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget));
                break;
            case VRenderParameters.DenoiseMode.QuikDenoise:
                {
                    cb.SetGlobalFloat("_DenoiseStrength", parameters.strength * 0.1f);
                    int k = Shader.PropertyToID("_VRenderTempDenoise");
                    cb.GetTemporaryRT(k, res.descriptor);
                    cb.SetGlobalFloat("_Flare", parameters.removeFlare);
                    cb.Blit(res, k, denoiseMaterial, 2);
                    cb.Blit(k, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget), denoiseMaterial, 0);
                    cb.ReleaseTemporaryRT(k);
                }
                break;
            case VRenderParameters.DenoiseMode.HisogramDenoise:
                {
                    int2 resolution = (int2)(math.float2(cam.pixelWidth, cam.pixelHeight) * (parameters.halfResolution ? 0.5f : 1));

                    int k = Shader.PropertyToID("_VRenderTempDenoise");
                    cb.GetTemporaryRT(k, res.descriptor);
                    cb.SetGlobalFloat("_Flare", 2);
                    cb.Blit(res, k, denoiseMaterial, 2);

                    cb.SetComputeTextureParam(cs_histoDenoise, 2, "_Variance", record);
                    cb.SetComputeTextureParam(cs_histoDenoise, 2, "_Denoised", denoised);
                    cb.SetComputeTextureParam(cs_histoDenoise, 2, "_History", k);
                    cb.SetComputeBufferParam(cs_histoDenoise, 2, "_HistogramBuffer", sampleHistogram);
                    cb.SetComputeIntParam(cs_histoDenoise, "_SubFrameIndex", subFrameIndex);
                    resolution = resolution / 4 + int2(resolution % 4 != 0);
                    cb.DispatchCompute(cs_histoDenoise, 2, (resolution.x / 8) + (resolution.x % 8 != 0 ? 1 : 0), (resolution.y / 8) + (resolution.y % 8 != 0 ? 1 : 0), 1);

                    cb.ReleaseTemporaryRT(k);
                    cb.Blit(denoised, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget));
                }
                break;
            default:
                break;
        }
    }

    int lastlayer = 0;
    void UpdateAccelerationStructure()
    {
        var acc = RTRegister.AccStruct(parameters.cullingMask);
#if UNITY_2020_2_OR_NEWER
        acc.Build();
#else
            acc.Update();
#endif
        cb.SetRayTracingAccelerationStructure(rtShader, "_RaytracingAccelerationStructure", acc);
        if (parameters.cullingMask != lastlayer)
        {
            lastlayer = parameters.cullingMask;
            ReRender();
        }
    }

    void UpdateCamera()
    {
        cb.SetGlobalMatrix("_P_Inv", GL.GetGPUProjectionMatrix(cam.projectionMatrix, false).inverse);
        cb.SetGlobalMatrix("_V_Inv", cam.cameraToWorldMatrix);
        //Debug.Log(cam.cameraToWorldMatrix);
        int2 resolution = (int2)(float2(cam.pixelWidth, cam.pixelHeight) * (parameters.halfResolution ? 0.5f : 1));
        cb.SetGlobalVector("_Pixel_WH", new Vector4(resolution.x, resolution.y, 1.0f / resolution.x, 1.0f / resolution.y));
        cb.SetGlobalVector("_WorldSpaceCameraPos", cam.transform.position);
    }

    void UpdateSkyBox()
    {
        string shaderName = "";
        if (RenderSettings.skybox != null)
        {
            shaderName = RenderSettings.skybox.shader.name;
        }
        if (LightManager.sunLight != null && LightManager.sunLight.atmoPreset != null)
        {
            Color lum;
            Vector3 dir;
            var sun = LightManager.sunLight;
            var atmo = sun.atmoPreset;
            atmo = sun.atmoPreset;
            lum = sun.color * sun.radiance * math.pow(10, 5.7f);
            dir = -sun.direction;

            if (atmo.GenerateLut(hash, cb, TLut, MSLut, lum, dir)) ReRender();

            atmo.GenerateVolumeSkyTexture(cb, VolumeLut, SkyLut, 0);

            cb.SetRayTracingIntParam(rtShader, "_Procedural", 0);

            cb.SetRayTracingTextureParam(rtShader, "_Skybox", SkyLut);
            cb.SetGlobalTexture("_TLut", TLut);
        }
        else if (shaderName == "Skybox/Panoramic")
        {
            cb.SetRayTracingIntParam(rtShader, "_Procedural", 1);
            Texture tex = RenderSettings.skybox.GetTexture("_MainTex");
            if (tex == null) tex = Texture2D.whiteTexture;
            cb.SetRayTracingTextureParam(rtShader, "_Skybox", tex);
            cb.SetRayTracingVectorParam(rtShader, "_Tint", RenderSettings.skybox.GetColor("_Tint"));
            cb.SetRayTracingFloatParam(rtShader, "_Exposure", RenderSettings.skybox.GetFloat("_Exposure"));
            cb.SetRayTracingFloatParam(rtShader, "_Rotation", RenderSettings.skybox.GetFloat("_Rotation"));
            cb.SetRayTracingVectorParam(rtShader, "_MainTex_HDR", RenderSettings.skybox.GetVector("_MainTex_HDR"));
            cb.SetGlobalTexture("_TLut", Texture2D.whiteTexture);
        }
        else
        {
            cb.SetRayTracingIntParam(rtShader, "_Procedural", 2);
            cb.SetRayTracingTextureParam(rtShader, "_Skybox", Texture2D.whiteTexture);
            cb.SetRayTracingVectorParam(rtShader, "_Tint", new Vector4(103, 128, 165) / 256);
            cb.SetRayTracingVectorParam(rtShader, "_MainTex_HDR", new Vector4(107, 91, 58) / 256);
            cb.SetGlobalTexture("_TLut", Texture2D.whiteTexture);
        }
    }

    bool enalbeFog = false;

    ComputeBuffer defaultBuffer = new ComputeBuffer(1, 1);
    RenderTexture defaultArray = new RenderTexture(
        new RenderTextureDescriptor(1, 1, RenderTextureFormat.R8, 0, 0) { dimension = TextureDimension.Tex2DArray, volumeDepth = 1 });

    void UpdateFog()
    {
        if (enalbeFog != parameters.enableFog)
        {
            enalbeFog = parameters.enableFog;
            ReRender();
        }
        cb.SetRayTracingIntParam(rtShader, "_EnableFog", enalbeFog ? 1 : 0);
        cb.SetGlobalInt("_EnableFog", enalbeFog ? 1 : 0);

        if (enalbeFog)
        {
            ComputeBuffer buffer;
            RenderTexture tex;
            SmokeManager.GetSmokeAtlas(out buffer, out tex);
            cb.SetGlobalBuffer("_MaterialArray", buffer);
            if (tex == null)
            {
                cb.SetGlobalTexture("_VolumeAtlas", defaultArray);
                cb.SetRayTracingTextureParam(rtShader, "_VolumeAtlas", defaultArray);
                cb.SetGlobalVector("_VolumeAtlasPixelSize", new Vector4(1.0f / defaultArray.width, 1.0f / defaultArray.height, 0, 0) * 2.1f);
            }
            else
            {
                cb.SetGlobalTexture("_VolumeAtlas", tex);
                cb.SetRayTracingTextureParam(rtShader, "_VolumeAtlas", tex);
                cb.SetGlobalVector("_VolumeAtlasPixelSize", new Vector4(1.0f / tex.width, 1.0f / tex.height, 0, 0) * 2.1f);
            }
            if (buffer != null)
            {
                cb.SetGlobalBuffer("_MaterialArray", buffer);
                cb.SetRayTracingBufferParam(rtShader, "_MaterialArray", buffer);
            }
            else
            {
                cb.SetGlobalBuffer("_MaterialArray", defaultBuffer);
                cb.SetRayTracingBufferParam(rtShader, "_MaterialArray", defaultBuffer);
            }
            cb.EnableShaderKeyword("_ENABLEFOG");
        }
        else
        {
            cb.DisableShaderKeyword("_ENABLEFOG");
        }
    }

    public void ReRender()
    {
        temporalFrameNumSwith = 1;
        FrameIndex = 0;
    }

    string _noGITag;
    int _maxDepth;
    VRenderParameters.DOF _dof;
    bool _cacheIrr;
    Matrix4x4 trans = Matrix4x4.zero;
    float IrradianceVolumeScale = -1;
    bool showIrr = false;
    void ParameterChanged()
    {
        parameters.maxDepth = clamp(parameters.maxDepth, 1, 16);
        parameters.DOFConfig.FocusDistance = max(parameters.DOFConfig.FocusDistance, 0.1f);
        parameters.DOFConfig.Aperture = max(parameters.DOFConfig.Aperture, 0.0f);
        if (_cacheIrr != parameters.cacheIrradiance ||
            _maxDepth != parameters.maxDepth ||
            _dof.Aperture != parameters.DOFConfig.Aperture || _dof.FocusDistance != parameters.DOFConfig.FocusDistance || _dof.bokehTexture != parameters.DOFConfig.bokehTexture ||
            _noGITag != parameters.noGITag)
        {
            _maxDepth = parameters.maxDepth;
            _dof = parameters.DOFConfig;
            _cacheIrr = parameters.cacheIrradiance;
            _noGITag = parameters.noGITag;
            ReRender();
        }
        if (trans != cam.cameraToWorldMatrix)
        {
            trans = cam.cameraToWorldMatrix;
            ReRender();
        }

        if (_cacheIrr && IrradianceVolumeScale != parameters.IrradianceVolumeScale)
        {
            IrradianceVolumeScale = parameters.IrradianceVolumeScale;
            ReRender();
        }

        if (_cacheIrr && parameters.debugMode == VRenderParameters.DebugMode.irradianceCache != showIrr)
        {
            showIrr = parameters.debugMode == VRenderParameters.DebugMode.irradianceCache;
            ReRender();
        }
    }

    void SetupFrameSamplesBuffer(MonoBehaviour host, CommandBuffer cb, int w, int h)
    {
        if (frameSamples == null || w != frameSamples.width || h != frameSamples.height)
        {
            if (frameSamples != null) frameSamples.Release();

            frameSamples = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBFloat, 0);
            frameSamples.enableRandomWrite = true;
            frameSamples.useMipMap = false;
            frameSamples.Create();

            denoised = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBHalf, 0);
            denoised.enableRandomWrite = true;
            denoised.useMipMap = false;
            denoised.Create();

            if (sampleHistogram != null) sampleHistogram.Release();
            sampleHistogram = new ComputeBuffer(w * h, sizeof(float) * 32);
        }
    }
}
