using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace HypnosRenderPipeline.RenderGraph
{

    [CustomEditor(typeof(HypnosRenderGraph))]
    public class RenderGraphInspector : Editor
    {
        bool node_fold = true;
        Vector2 node_scroll = Vector2.zero;

        bool edge_fold = true;
        bool code_fold = false;
        Vector2 edge_scroll = Vector2.zero;
        Vector2 code_scroll = Vector2.zero;
        public override void OnInspectorGUI()
        {
            //base.OnInspectorGUI();
            var info = target as HypnosRenderGraph;

            node_fold = EditorGUILayout.BeginFoldoutHeaderGroup(node_fold, "Nodes    " + info.nodes.Count);
            if (node_fold)
            {
                var rect = EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(" Name", GUILayout.Width(EditorGUIUtility.currentViewWidth / 2.2f));
                EditorGUILayout.LabelField(" In", GUILayout.Width(50));
                EditorGUILayout.LabelField(" Out", GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
                EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.1f));

                rect = EditorGUILayout.BeginVertical();
                node_scroll = GUILayout.BeginScrollView(node_scroll, false, false, new GUILayoutOption[] {  GUILayout.Height(Mathf.Min(21 * info.nodes.Count, 210)) });
                foreach (var node in info.nodes)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(node.nodeName, GUILayout.Width(EditorGUIUtility.currentViewWidth / 2.2f));
                    var edge_info = info.SearchNodeInDic(node);
                    int a = 0, b = 0;
                    if (edge_info != null)
                    {
                        a = edge_info.Item1.Count;
                        b = edge_info.Item2.Count;
                    }
                    EditorGUILayout.LabelField(a.ToString(), GUILayout.Width(50));
                    EditorGUILayout.LabelField(b.ToString(), GUILayout.Width(50));
                    EditorGUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.1f));
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            edge_fold = EditorGUILayout.BeginFoldoutHeaderGroup(edge_fold, "Edges    " + info.edges.Count);
            if (edge_fold)
            {
                var rect = EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("", GUILayout.Width(40));
                EditorGUILayout.LabelField(" Node", GUILayout.Width(EditorGUIUtility.currentViewWidth / 2.2f - 30));
                EditorGUILayout.LabelField(" Pin", GUILayout.Width(EditorGUIUtility.currentViewWidth / 2.2f - 30));
                EditorGUILayout.EndHorizontal();
                EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.1f));

                edge_scroll = GUILayout.BeginScrollView(edge_scroll, false, false, new GUILayoutOption[] { GUILayout.Height(Mathf.Min(42 * info.edges.Count, 210)) });
                foreach (var edge in info.edges)
                {
                    rect = EditorGUILayout.BeginVertical();
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("From:", GUILayout.Width(40));
                    EditorGUILayout.LabelField(edge.output.node.nodeName, GUILayout.Width(EditorGUIUtility.currentViewWidth / 2.2f - 30));
                    EditorGUILayout.LabelField(edge.output.name, GUILayout.Width(EditorGUIUtility.currentViewWidth / 2.2f - 30));
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("To:", GUILayout.Width(40));
                    EditorGUILayout.LabelField(edge.input.node.nodeName, GUILayout.Width(EditorGUIUtility.currentViewWidth / 2.2f - 30));
                    EditorGUILayout.LabelField(edge.input.name, GUILayout.Width(EditorGUIUtility.currentViewWidth / 2.2f - 30));
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.1f));
                }
                GUILayout.EndScrollView();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (GUILayout.Button("ReCompile")) {
                HRGCompiler.Compile(info);
            }

            code_fold = EditorGUILayout.BeginFoldoutHeaderGroup(code_fold, "Generated Code");
            if (code_fold)
            {
                code_scroll = GUILayout.BeginScrollView(code_scroll, false, false, new GUILayoutOption[] { GUILayout.Height(210) });
                var rect = EditorGUILayout.BeginVertical();
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextArea(info.code);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndVertical();
                EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.1f));
                GUILayout.EndScrollView();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        [OnOpenAsset(0)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            try
            {
                var path = AssetDatabase.GetAssetPath(instanceID);
                if (AssetDatabase.LoadAssetAtPath<HypnosRenderGraph>(path) != null)
                {
                    RenderGraphViewWindow.Create().Load(path);
                    return true;
                }
                return false;
            }
            catch(System.Exception e)
            {
                Debug.LogError("Load faild: " + e.Message);
                return false;
            }
        }
    }
}