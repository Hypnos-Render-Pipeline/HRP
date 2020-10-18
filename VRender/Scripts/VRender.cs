using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using static Unity.Mathematics.math;
using UnityEngine.Rendering;
using Unity.Mathematics;
using System.Collections.Generic;
using UnityEditor;

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


    [System.Serializable]
    public struct DOF
    {
        [Min(0)]
        public float FocusDistance;
        [Range(0, 10)]
        public float Aperture;
    }

    public DOF DOFConfig = new DOF { FocusDistance = 10f, Aperture = 0f };

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
    public enum DebugMode { none, varience, albedo, normal, irradianceCache, allInDock };

    [Space(10.0f)]

    public DebugMode debugMode = DebugMode.none;

    [System.Serializable]
    public enum Mode { none, SmartDenoise, Another };
    [Space(15)]
    [Header("Quick deniose")]
    public Mode denosieMode = Mode.none;
    [Range(0.01f, 1)]
    public float strength = 0.1f;
}


public class VRender : IDisposable
{
    public VRenderParameters parameters;

    HypnosRenderPipeline.BNSLoader bnsLoader;
    RayTracingShader rtShader;
    Material blitMat;
    Material denoiseMaterial;
    Texture2D ssLut;

    public Camera cam;

    CommandBuffer cb;
    ComputeBuffer cb_LightList;

    RenderTexture frameSamples;
    RenderTexture albedo, normal;

    ComputeShader cs_clearVolume;
    RenderTexture tex3D_iv;

    RenderTexture history = null, record = null;

    public VRender(Camera cam, VRenderParameters parameters = null)
    {
        cb = GetCB(cam, "RayTrace", CameraEvent.BeforeImageEffects);
        cam.depthTextureMode |= DepthTextureMode.DepthNormals | DepthTextureMode.Depth;

        rtShader = Resources.Load<RayTracingShader>("RayTracer");
        blitMat = new Material(Shader.Find("Hidden/VRenderBlit"));
        denoiseMaterial = new Material(Shader.Find("Hidden/QuickDenoise"));
        ssLut = Resources.Load<Texture2D>("Textures/Random Lut/RdLut");
        bnsLoader = HypnosRenderPipeline.BNSLoader.instance;
        cb_LightList = new ComputeBuffer(100, HypnosRenderPipeline.LightListGenerator.lightStructSize);
        cs_clearVolume = Resources.Load<ComputeShader>("ClearVolume");
        tex3D_iv = new RenderTexture(128, 64, 0);
        tex3D_iv.dimension = TextureDimension.Tex3D;
        tex3D_iv.volumeDepth = 128;
        tex3D_iv.format = RenderTextureFormat.ARGBHalf;
        tex3D_iv.autoGenerateMips = false;
        tex3D_iv.enableRandomWrite = true;
        tex3D_iv.Create();

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

        int2 resolution = (int2)(math.float2(cam.pixelWidth, cam.pixelHeight) * (parameters.halfResolution ? 0.5f : 1));

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
        if (parameters.cacheIrradiance && FrameIndex == 1)
        {
            cb.SetComputeTextureParam(cs_clearVolume, 0, "_Volume", tex3D_iv);
            cb.DispatchCompute(cs_clearVolume, 0, 128 / 4, 64 / 4, 128 / 4);
        }

        cb.SetGlobalTexture("_Sobol", bnsLoader.tex_sobol);
        cb.SetGlobalTexture("_ScramblingTile", bnsLoader.tex_scrambling);
        cb.SetGlobalTexture("_RankingTile", bnsLoader.tex_rankingTile);
        cb.SetGlobalTexture("_RdLut", ssLut);
        cb.SetRayTracingIntParam(rtShader, "_CacheIrradiance", parameters.cacheIrradiance ? (parameters.debugMode == VRenderParameters.DebugMode.irradianceCache ? 2 : 1) : 0);
        cb.SetRayTracingVectorParam(rtShader, "_IVScale", Vector4.one);
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

        switch (parameters.debugMode)
        {
            case VRenderParameters.DebugMode.none:
            case VRenderParameters.DebugMode.irradianceCache:
                BlitResultToScreen(history);
                break;
            case VRenderParameters.DebugMode.varience:
                cb.Blit(record, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget), blitMat, 0);
                break;
            case VRenderParameters.DebugMode.albedo:
                cb.Blit(new RenderTargetIdentifier(BuiltinRenderTextureType.GBuffer0), new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget));
                break;
            case VRenderParameters.DebugMode.normal:
                cb.Blit(null, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget), blitMat, 1);
                break;
            case VRenderParameters.DebugMode.allInDock:
                BlitResultToScreen(history);
                cb.Blit(record, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget), blitMat, 2);
                cb.Blit(new RenderTargetIdentifier(BuiltinRenderTextureType.GBuffer0), albedo);
                cb.Blit(albedo, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget), blitMat, 3);
                cb.Blit(null, normal, blitMat, 1);
                cb.Blit(normal, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget), blitMat, 4);
                break;
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
            case VRenderParameters.Mode.none:
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
            case VRenderParameters.Mode.SmartDenoise:
                cb.SetGlobalFloat("_DenoiseStrength", parameters.strength * 0.1f);
                cb.Blit(res, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget), denoiseMaterial, 0);
                break;
            case VRenderParameters.Mode.Another:

                break;
            default:
                break;
        }
    }

    int lastlayer = 0;
    void UpdateAccelerationStructure()
    {
        var acc = HypnosRenderPipeline.RTRegister.AccStruct(parameters.cullingMask);
        acc.Build();
        acc.Update();
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
        cb.SetGlobalVector("_Pixel_WH", new Vector4(cam.pixelWidth, cam.pixelHeight) * (parameters.halfResolution ? 0.5f : 1));
        cb.SetGlobalVector("_WorldSpaceCameraPos", cam.transform.position);
    }

    void UpdateSkyBox()
    {
        string shaderName = "";
        if (RenderSettings.skybox != null)
        {
            shaderName = RenderSettings.skybox.shader.name;
        }
        if (shaderName == "Skybox/Panoramic")
        {
            cb.SetRayTracingIntParam(rtShader, "_Procedural", 0);
            Texture tex = RenderSettings.skybox.GetTexture("_MainTex");
            if (tex == null) tex = Texture2D.whiteTexture;
            cb.SetRayTracingTextureParam(rtShader, "_Skybox", tex);
            cb.SetRayTracingVectorParam(rtShader, "_Tint", RenderSettings.skybox.GetColor("_Tint"));
            cb.SetRayTracingFloatParam(rtShader, "_Exposure", RenderSettings.skybox.GetFloat("_Exposure"));
            cb.SetRayTracingFloatParam(rtShader, "_Rotation", RenderSettings.skybox.GetFloat("_Rotation"));
            cb.SetRayTracingVectorParam(rtShader, "_MainTex_HDR", RenderSettings.skybox.GetVector("_MainTex_HDR"));
        }
        else
        {
            cb.SetRayTracingIntParam(rtShader, "_Procedural", 1);
            cb.SetRayTracingTextureParam(rtShader, "_Skybox", Texture2D.whiteTexture);
            cb.SetRayTracingVectorParam(rtShader, "_Tint", new Vector4(103, 128, 165) / 256);
            cb.SetRayTracingVectorParam(rtShader, "_MainTex_HDR", new Vector4(107, 91, 58) / 256);
        }
    }

    void UpdateFog()
    {
        if (enableFog)
        {
            cb.SetRayTracingIntParam(rtShader, "_enableGlobalFog", 1);
            cb.SetRayTracingVectorParam(rtShader, "_globalFogParameter", float4(0, fogAbsorb, fogScatter, fogG));
        }
        else
        {
            cb.SetRayTracingIntParam(rtShader, "_enableGlobalFog", 0);
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
    void ParameterChanged()
    {
        parameters.maxDepth = clamp(parameters.maxDepth, 1, 16);
        parameters.DOFConfig.FocusDistance = max(parameters.DOFConfig.FocusDistance, 0.1f);
        parameters.DOFConfig.Aperture = max(parameters.DOFConfig.Aperture, 0.0f);
        if (_cacheIrr != parameters.cacheIrradiance ||
            _maxDepth != parameters.maxDepth ||
            _dof.Aperture != parameters.DOFConfig.Aperture || _dof.FocusDistance != parameters.DOFConfig.FocusDistance ||
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

            albedo = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, 0);
            albedo.useMipMap = false;
            albedo.Create();

            normal = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBHalf, 0);
            normal.useMipMap = false;
            normal.Create();

        }
    }

    bool enableFog = false;
    float fogAbsorb;
    float fogScatter;
    float fogG;
    public void Fog(bool enable, float absorb = 0, float scatter = 1, float G = 0.5f)
    {
        enableFog = enable;
        fogAbsorb = absorb;
        fogScatter = max(0, scatter) * 0.01f;
        fogG = saturate(G);
        ReRender();
    }
}
