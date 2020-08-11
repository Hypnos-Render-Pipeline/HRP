using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainMesh))]
public class TerrainEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        base.OnInspectorGUI();
        if (EditorGUI.EndChangeCheck())
        {
            (target as TerrainMesh).Generate();
        }
    }
}
