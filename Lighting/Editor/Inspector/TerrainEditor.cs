using UnityEditor;

namespace HypnosRenderPipeline
{

    [CustomEditor(typeof(HRPTerrain))]
    public class TerrainEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            base.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                (target as HRPTerrain).Generate();
            }
        }
    }
}
