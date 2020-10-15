using System.Collections;
using System.Collections.Generic;
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
        Transform trans = target as Transform;
        if (p != trans.position || r != trans.rotation || s != trans.lossyScale)
        {
            p = trans.position;
            r = trans.rotation;
            s = trans.lossyScale;
            HypnosRenderPipeline.RTRegister.SceneChanged();
        }

        EditorGUI.BeginChangeCheck();

        var pos_prop = serializedObject.FindProperty("m_LocalPosition");
        var rot_prop = serializedObject.FindProperty("m_LocalRotation");
        var scal_prop = serializedObject.FindProperty("m_LocalScale");
        
        var pos = EditorGUILayout.Vector3Field("Position", pos_prop.vector3Value);
        var rot = EditorGUILayout.Vector3Field("Rotation", rot_prop.quaternionValue.eulerAngles);
        var scal = EditorGUILayout.Vector3Field("Scale", scal_prop.vector3Value);


        if (EditorGUI.EndChangeCheck())
        {
            if (trans.gameObject.activeInHierarchy == true)
            {
                HypnosRenderPipeline.RTRegister.SceneChanged();
            }
            pos_prop.vector3Value = pos;
            rot_prop.quaternionValue = Quaternion.Euler(rot.x, rot.y, rot.z);
            scal_prop.vector3Value = scal;
            serializedObject.ApplyModifiedProperties();
        }
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.HelpBox("This Inspector has been overrided", MessageType.Info);
        EditorGUI.EndDisabledGroup();
    }
}
