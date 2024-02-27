using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Transform))]
public class TransformGUI : Editor
{
    private void OnEnable()
    {
        Transform trans = target as Transform;
        p = trans.position;
        r = trans.rotation;
        s = trans.lossyScale;
    }

    Vector3 p;
    Quaternion r;
    Vector3 s;


    public override void OnInspectorGUI()
    {
        Editor editor = CreateEditor(target, Assembly.GetAssembly(typeof(Editor)).GetType("UnityEditor.TransformInspector"));
        editor.OnInspectorGUI();
        DestroyImmediate(editor);

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.HelpBox("This Inspector has been override", MessageType.Info);
        EditorGUI.EndDisabledGroup();

        Transform trans = target as Transform;
        if (p != trans.position || r != trans.rotation || s != trans.lossyScale)
        {
            p = trans.position;
            r = trans.rotation;
            s = trans.lossyScale;
            if (trans.gameObject.activeInHierarchy == true)
                HypnosRenderPipeline.RTRegister.SceneChanged();
        }
    }
}
