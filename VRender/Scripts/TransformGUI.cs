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

    Editor m_cacheEditor;

    public override void OnInspectorGUI()
    {
        if (m_cacheEditor == null)
            m_cacheEditor = CreateEditor(target, Assembly.GetAssembly(typeof(Editor)).GetType("UnityEditor.TransformInspector", true));
        m_cacheEditor.OnInspectorGUI();

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.HelpBox("This Inspector has been overrided", MessageType.Info);
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
