using System;
using System.Linq.Expressions;
using UnityEditor;
using UnityEngine;

namespace HypnosRenderPipeline
{

    [CustomEditor(typeof(HRPTerrain))]
    public class HRPTerrainEditor : Editor
    {
        bool editTerrain = false;

        Quaternion m_downRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

        public override void OnInspectorGUI()
        {
            var terrain = target as HRPTerrain;
            EditorGUI.BeginChangeCheck();
            base.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                terrain.Generate();
            }

            EditorGUI.BeginDisabledGroup(terrain.terrainData == null);

            var oriColor = GUI.color;
            GUI.color = editTerrain ? Color.gray : Color.white;
            if (GUILayout.Button(editTerrain ? "Done" : "Edit Terrain Data"))
            {
                editTerrain = !editTerrain;
                if (editTerrain)
                {
                    EnterEdit();
                }
                else
                {
                    ExitEdit();
                }
                SceneView.lastActiveSceneView.Repaint();
            }
            GUI.color = oriColor;
            
            EditorGUI.EndDisabledGroup();

            if (editTerrain)
            {
                HRPTerrainDataEditor.DrawInspector(terrain.terrainData);
            }
        }

        void EnterEdit()
        {
            var terrain = target as HRPTerrain;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.LookAt(terrain.terrainData.centerV3, m_downRotation, terrain.terrainData.maxSize);
                SceneView.lastActiveSceneView.isRotationLocked = true;
            }
            editTerrain = true;
            ActiveEditorTracker.sharedTracker.isLocked = true;
        }

        void ExitEdit()
        {
            var terrain = target as HRPTerrain;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.isRotationLocked = false;
            editTerrain = false;
            ActiveEditorTracker.sharedTracker.isLocked = false;
        }


        void OnSceneGUI()
        {
            if (!editTerrain) return;
            var terrain = target as HRPTerrain;
            var data = terrain.terrainData;
            if (data == null) return;

            Vector3 center = new Vector3(data.center.x, 0, data.center.y);
            DrawRect(m_downRotation, center, data.size, new Color(0.5f, 0.5f, 1f, 1f), data.tileCount);

        }

        void OnEnable()
        {

            var sizeSliderMethod = typeof(Handles).
                       GetMethod(
                               "SizeSlider",
                               System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                               null,
                               System.Reflection.CallingConventions.Any,
                               new[] { typeof(Vector3), typeof(Vector3), typeof(float) },
                               null);
            var position = Expression.Parameter(typeof(Vector3), "position");
            var direction = Expression.Parameter(typeof(Vector3), "direction");
            var size = Expression.Parameter(typeof(float), "size");
            var call = Expression.Call(sizeSliderMethod, position, direction, size);
            m_sizeSlider = Expression.Lambda<Func<Vector3, Vector3, float, float>>(call, position, direction, size).Compile();
        }

        void OnDisable()
        {
            ExitEdit();
        }

        Func<Vector3, Vector3, float, float> m_sizeSlider;

        void DrawRect(Quaternion rotation, Vector3 position, Vector2 size, Color color, Vector2Int gridNum)
        {
            var oriColor = Handles.color;
            Handles.color = color;

            Vector3 up = rotation * Vector3.up;
            Vector3 right = rotation * Vector3.right;

            float halfWidth = 0.5f * size.x;
            float halfHeight = 0.5f * size.y;

            Vector3 topRight = position + up * halfHeight + right * halfWidth;
            Vector3 bottomRight = position - up * halfHeight + right * halfWidth;
            Vector3 bottomLeft = position - up * halfHeight - right * halfWidth;
            Vector3 topLeft = position + up * halfHeight - right * halfWidth;

            for (int i = 0; i <= gridNum.x; i++)
            {
                float t = (float)i / gridNum.x;
                Handles.DrawLine(Vector3.Lerp(topLeft, topRight, t), Vector3.Lerp(bottomLeft, bottomRight, t));
            }
            for (int i = 0; i <= gridNum.y; i++)
            {
                float t = (float)i / gridNum.y;
                Handles.DrawLine(Vector3.Lerp(bottomLeft, topLeft, t), Vector3.Lerp(bottomRight, topRight, t));
            }

            Handles.color = oriColor;
        }
    }
}
