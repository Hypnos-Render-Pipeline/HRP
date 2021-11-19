using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HypnosRenderPipeline.Tools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HypnosRenderPipeline
{
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class HRPSmoke : MonoBehaviour
    {
        public int id { get; internal set; } = -1;

        public bool usedInVrender = false;

        [HideInInspector]
        public MeshRenderer meshRenderer;
        [HideInInspector]
        public MeshFilter meshFilter;

        private void OnEnable()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            meshFilter = GetComponent<MeshFilter>();

            meshFilter.sharedMesh = MeshWithType.cube;
        }

        private void Update()
        {
#if UNITY_EDITOR
            var a = EditorApplication.update.GetInvocationList();
            var vrender_on = a.FirstOrDefault((b) => { return b.Method.Module.Name == "HypnosRenderPipeline.VRender.dll"; }) != null;
            if (meshRenderer.sharedMaterial.shader.name == "HRP/Smoke") {
                if (vrender_on)
                    meshRenderer.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.DynamicTransform;
                else
                    meshRenderer.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;
            }
            else
            {
                meshRenderer.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;
                if (vrender_on)
                    Debug.LogError("VRender only support smoke using Shader \"HRP/Smoke\"! Given + \"" + meshRenderer.sharedMaterial.shader.name + "\".");
            }
#endif
        }

        private void OnDrawGizmos()
        {
            var mat = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = mat;
        }
    }


#if UNITY_EDITOR
    [CustomEditor(typeof(HRPSmoke))]
    [CanEditMultipleObjects]
    public class HRPSmokeEditor : Editor
    {
        SerializedObject m_DataObject;
        public override void OnInspectorGUI()
        {
            var smoke = target as HRPSmoke;
            m_DataObject = new SerializedObject(smoke);

            EditorGUI.BeginChangeCheck();

            bool can_in_vr = smoke.meshRenderer.sharedMaterial.shader.name == "HRP/Smoke";
            EditorGUI.BeginDisabledGroup(!can_in_vr);
            bool used = EditorGUILayout.Toggle("Used In Vrender", smoke.usedInVrender);
            EditorGUI.EndDisabledGroup();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(smoke, "Change Smoke Parameter(s)");
                smoke.usedInVrender = used;
                RTRegister.UndoRedoCallback();
            }
            if (!can_in_vr)
                EditorGUILayout.HelpBox("VRender only support smoke using Shader \"HRP/Smoke\"!", MessageType.Warning);
        }
    }
#endif
}
