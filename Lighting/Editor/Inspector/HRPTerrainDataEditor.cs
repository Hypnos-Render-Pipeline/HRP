using UnityEditor;
using UnityEngine;

namespace HypnosRenderPipeline
{

    [CustomEditor(typeof(HRPTerrainData))]
    public class HRPTerrainDataEditor : Editor
    {

        public override void OnInspectorGUI()
        {
            DrawInspector(target as HRPTerrainData);
        }

        public static void DrawInspector(HRPTerrainData data)
        {
            EditorGUI.BeginChangeCheck();
            var tc = EditorGUILayout.Vector2IntField("Tile Count", data.tileCount);
            var ts = EditorGUILayout.Slider("Tile Size", data.tileSize, 64, 256);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(data, "Change Terrain data");
                data.tileCount = tc;
                data.tileSize = ts;
                data.height = new Texture2D[data.tileCount.x * data.tileCount.y];
                EditorUtility.SetDirty(data);
            }
            EditorGUI.BeginChangeCheck();

            data.heightRange = EditorGUILayout.Vector2Field("Height Range", data.heightRange);

            for (int i = 0; i < data.tileCount.x; i++)
            {
                for (int j = 0; j < data.tileCount.y; j++)
                {
                    data.height[i * data.tileCount.y + j] = (Texture2D)EditorGUILayout.ObjectField(i.ToString() + ", " + j.ToString(), data.height[i * data.tileCount.y + j], typeof(Texture2D), false);
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(data);
            }
        }
    }
}
