using UnityEditor;

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
                EditorUtility.SetDirty(data);
            }
        }
    }
}
