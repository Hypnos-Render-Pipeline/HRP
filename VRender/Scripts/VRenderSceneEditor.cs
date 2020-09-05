using HypnosRenderPipeline;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

class VRenderForScene
{
    public static readonly VRenderForScene instance = new VRenderForScene();

    VRender vRender;

    public bool enable = false;
    public bool performance = true;
    public bool cacheIrradiance = false;
    public int maxBounce = 4;
    public float nearPlane = 0.3f;
    public VRenderParameters.DOF dof;
    public VRenderParameters.LightSetting lightSetting;
    public LayerMask layer = -1;

    Texture2D rtxon;
    Material mat;

    VRenderForScene()
    {
        EditorApplication.playModeStateChanged += OnChangeState;
        EditorSceneManager.sceneOpening += ChangeScene;

        dof.Aperture = 0;
        dof.FocusDistance = 10;
        lightSetting = new VRenderParameters.LightSetting();
        lightSetting.attenuationCurve = AnimationCurve.Linear(0, 1, 1, 0);
        rtxon = Resources.Load<Texture2D>("RTXON");
        mat = new Material(Shader.Find("Unlit/Transparent"));
    }
    ~VRenderForScene()
    {
        Dispose();
        EditorApplication.update -= OnRender;
        SceneView.duringSceneGui -= OnRepaint;
        EditorSceneManager.sceneOpening -= ChangeScene;
        EditorApplication.playModeStateChanged -= OnChangeState;
        RTRegister.ClearMaterialCache();
    }

    void ChangeScene(string path, OpenSceneMode mode)
    {
        instance.enable = false;
        Dispose();
        EditorApplication.update -= OnRender;
        SceneView.duringSceneGui -= OnRepaint;
        RTRegister.ClearMaterialCache();
    }

    public void Dispose()
    {
        if (instance.vRender != null)
        {
            instance.vRender.Dispose();
        }
        instance.vRender = null;
        RTRegister.ClearMaterialCache();
    }

    static public void CreateVRenderForSceneCamera(Camera cam)
    {
        instance.Dispose();
        var vr = instance.vRender = new VRender(cam);
        vr.parameters.temporalFrameNum = 9999999;
        vr.parameters.preferFrameRate = true;
        vr.parameters.halfResolution = true;
        vr.parameters.upsample = true;
        vr.parameters.denosieMode = VRenderParameters.Mode.SmartDenoise;
        vr.parameters.strength = 0.15f;
        vr.parameters.nearPlane = 0.3f;
    }
    
    private void OnChangeState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
        {
            enable = false;
            instance.Dispose();
            EditorApplication.update -= OnRender;
            SceneView.duringSceneGui -= OnRepaint;
            RTRegister.ClearMaterialCache();
        }
    }
    
    public static void OnRender()
    {
        if (instance.vRender != null) instance.vRender.ClearCB();

        if (!instance.enable) return;
        
        if (instance.vRender == null || instance.vRender.cam != SceneView.lastActiveSceneView.camera)
        {
            CreateVRenderForSceneCamera(SceneView.lastActiveSceneView.camera);
        }

        instance.vRender.parameters.halfResolution = instance.performance;
        instance.vRender.parameters.cacheIrradiance = instance.cacheIrradiance;
        instance.vRender.parameters.maxDepth = instance.maxBounce;
        instance.vRender.parameters.DOFConfig = instance.dof;
        instance.vRender.parameters.lightSetting.Set(instance.lightSetting);
        instance.vRender.parameters.nearPlane = instance.nearPlane;
        instance.vRender.parameters.cullingMask = instance.layer;
        instance.vRender.Render();
        
        SceneView.lastActiveSceneView.Repaint();
    }

    [MenuItem("Tools/VRender/Enable VRender on scene view %#V")]
    static void EnableVRender()
    {
        var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
        if (apis[0] == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11)
        {
            Debug.LogError("Current Graphics API is " + apis[0] + ", please change to D3DX12");
            return;
        }

        var enable = VRenderForScene.instance.enable;
        VRenderForScene.instance.enable = !enable;
        if (!enable)
        {
            EditorApplication.update += OnRender;
            SceneView.duringSceneGui += OnRepaint;
        }
        else
        {
            EditorApplication.update -= OnRender;
            SceneView.duringSceneGui -= OnRepaint;
            VRenderForScene.instance.Dispose();
        }
        SceneView.lastActiveSceneView.Repaint();
        VRenderForScene.instance.rtxon = Resources.Load<Texture2D>("RTXON");
        VRenderForScene.instance.mat = new Material(Shader.Find("Unlit/Transparent"));
    }

    bool flodout = false;
    static void OnRepaint(SceneView sceneView)
    {
        if (Event.current.type == EventType.MouseDrag
            && Event.current.button == 0 && Selection.gameObjects.Length != 0)
        {
            RTRegister.SceneChanged();
        }
        Handles.BeginGUI();
        EditorGUI.DrawPreviewTexture(new Rect(0, 1, instance.rtxon.width / 2, instance.rtxon.height / 2), instance.rtxon, instance.mat, ScaleMode.ScaleToFit);
        GUILayoutUtility.GetRect(instance.rtxon.width / 2, instance.rtxon.height / 2 - 19, new GUILayoutOption[] { GUILayout.ExpandWidth(false) }); 
        var rect = EditorGUILayout.BeginVertical(new GUILayoutOption[] { GUILayout.Width(instance.flodout ? 270 : 60), GUILayout.Height(instance.flodout ? 160 : 20) });

        GUIStyle fontStyle = new GUIStyle();
        fontStyle.normal.background = null;
        fontStyle.normal.textColor = Color.white;
        fontStyle.active.textColor = Color.white;
        fontStyle.hover.textColor = Color.white;
        fontStyle.focused.textColor = Color.white;
        fontStyle.fontStyle = FontStyle.Normal;
        fontStyle.fontSize += 13;
        fontStyle.alignment = TextAnchor.MiddleCenter;

        GUI.backgroundColor = new Color(1, 1, 1, 0.2f);
        instance.flodout = EditorGUILayout.BeginFoldoutHeaderGroup(instance.flodout, "Settings", fontStyle);

        EditorGUI.DrawRect(rect, instance.flodout ? new Color(0f, 0f, 0f, 0.5f) : new Color(0f, 0f, 0f, 0.5f));
        GUI.backgroundColor = new Color(1, 1, 1, 0.7f);

        if (instance.flodout)
        {
            instance.performance = EditorGUILayout.Toggle("Performance First", instance.performance);
            instance.cacheIrradiance = EditorGUILayout.Toggle("Cache Irradiance", instance.cacheIrradiance);
            instance.maxBounce = EditorGUILayout.IntSlider("Max bounce", instance.maxBounce, 1, 16);
            EditorGUI.BeginChangeCheck();
            instance.nearPlane = EditorGUILayout.Slider("Near Clip Dis", instance.nearPlane, 0, 20);
            if (EditorGUI.EndChangeCheck())
            {
                instance.vRender.ReRender();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Depth of field");

            instance.dof.FocusDistance = EditorGUILayout.FloatField("Focus Distance", instance.dof.FocusDistance);
            instance.dof.Aperture = EditorGUILayout.Slider("Aperture", instance.dof.Aperture, 0, 1);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Light settings");

            instance.lightSetting.useAttenuationCurve = EditorGUILayout.Toggle("Use Attenuation Cureve", instance.lightSetting.useAttenuationCurve);
            instance.lightSetting.attenuationCurve = EditorGUILayout.CurveField("Attenuation Cureve", instance.lightSetting.attenuationCurve);

            EditorGUILayout.Space(10);

            instance.layer = DrawLayerMaskField(instance.layer);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.EndVertical();
        Handles.EndGUI();
    }

    static LayerMask DrawLayerMaskField(LayerMask layerMask)
    {
        int layer_int = layerMask.value;
        List<string> layers = new List<string>();
        int l = 0;
        int layer = 0;
        for (int i = 0; i < 32; i++)
        {
            var name = LayerMask.LayerToName(i);
            if (name != "")
            {
                layers.Add(name);
                if ((layer_int & (1 << i)) != 0)
                {
                    layer += 1 << l;
                }
                l++;
            }
        }
        if (layer_int == -1 || layer_int == 0)
        {
            layer = layer_int;
        }
        layer = EditorGUILayout.MaskField(label: "Culling Mask", layer, layers.ToArray());
        if (layer == -1 || layer == 0)
        {
            layer_int = layer;
        }
        else
        {
            int accLayer = 0;
            for (int i = 0; i < layers.Count; i++)
            {
                if ((layer & (1 << i)) != 0)
                {
                    accLayer += 1 << LayerMask.NameToLayer(layers[i]);
                }
            }
            layer_int = accLayer;
        }
        return layer_int;
    }

    [MenuItem("Tools/VRender/Force VRender to repaint &%#V")]
    static void RepaintVRender()
    {
        if (instance.vRender != null)
            instance.vRender.ReRender();
    }
}

public class SaveScene : UnityEditor.AssetModificationProcessor
{
    static public void OnWillSaveAssets(string[] names)
    {

        foreach (string name in names)
        {
            if (name.EndsWith(".unity"))
            {
                RTRegister.ClearMaterialCache();
            }
        }
    }
}