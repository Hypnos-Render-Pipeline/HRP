using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Mathematics;
using System;
using UnityEditor;

#if UNITY_EDITOR

[ExecuteInEditMode]
[DisallowMultipleComponent]
public class VRenderCamera : MonoBehaviour
{
    public bool render = true;

    [HideInInspector]
    public VRender vRender;

    public VRenderParameters parameters;

    void Awake()
    {
    }

    private void OnEnable()
    {
        vRender = new VRender(GetComponent<Camera>(), parameters);
    }

    private void OnDisable()
    {
        vRender.Dispose();
    }

    void Update()
    {
        if (render)
            vRender.Render(this);
        else
            vRender.ClearCB();
    }

    public void OutputImage(bool denosied, bool hdr, string name)
    {
        vRender.OutputImage(this, name);
    }

    public void Moved()
    {
        vRender.ReRender();
    }
}

[CustomEditor(typeof(VRenderCamera))]
public class VRenderCameraEditor : Editor
{
    bool isplaying;
    public class SaveImageWindow : EditorWindow
    {
        public string file_name = "noname";
        VRenderCameraEditor inspector;

        static public void ShowWindow(VRenderCameraEditor ins)
        {
            SaveImageWindow myWindow = (SaveImageWindow)EditorWindow.GetWindow<SaveImageWindow>("Save Image");
            myWindow.minSize = myWindow.maxSize = new Vector2(400, 200);
            myWindow.inspector = ins;
            myWindow.Show();
        }

        private void OnGUI()
        {
            if (EditorApplication.isPlaying == false)
            {
                Close();
            }
            VRenderCamera vcam = inspector.target as VRenderCamera;

            file_name = EditorGUILayout.TextField("File name:", file_name);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
            {
                vcam.OutputImage(false, false, file_name);
                Debug.Log("Image is in the 'StreamingAssets/VRenderOutput.'");
                Close();
            }
            GUILayout.EndHorizontal();
        }
    }



    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        VRenderCamera vcam = (VRenderCamera)target;

        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);

        if (GUILayout.Button("Save Image as ..."))
        {
            SaveImageWindow.ShowWindow(this);
        }
        EditorGUI.EndDisabledGroup();
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Save image only work in playing mode.", MessageType.Info);
        }
        if (vcam.vRender.parameters.cacheIrradiance)
        {
            EditorGUILayout.HelpBox("\"Cache Irradiance\" will decrease noise but introduce bias.\nRecommend to use it when render indoor scene.", MessageType.Warning);
        }
    }
}

#else


public class VRenderCamera : MonoBehaviour { }

#endif