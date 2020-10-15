using HypnosRenderPipeline.RenderPass;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace HypnosRenderPipeline.RenderGraph
{
    internal class RenderGraphNodeSeacher : ScriptableObject, ISearchWindowProvider
    {
        EditorWindow m_editorWindow;
        RenderGraphView m_graphView;
        RenderGraphEditorView m_renderGraphEditorView;

        public void Init(RenderGraphView graphView, EditorWindow editorWindow, RenderGraphEditorView renderGraphEditorView)
        {
            m_graphView = graphView;
            m_editorWindow = editorWindow;
            m_renderGraphEditorView = renderGraphEditorView;
        }

        void BuildTree(int level, List<Tuple<string, Type>> nodes, List<SearchTreeEntry> tree)
        {
            Dictionary<string, List<Tuple<string, Type>>> next_paths = new Dictionary<string, List<Tuple<string, Type>>>();
            List<Type> this_level_nodes = new List<Type>();
            foreach (var node in nodes)
            {
                var path_splits = node.Item1 == "" ? new string[0] : node.Item1.Split('/');
                if (path_splits.Length == level)
                {
                    this_level_nodes.Add(node.Item2);
                }
                else
                {
                    string next_path = path_splits[level];
                    if (!next_paths.ContainsKey(next_path))
                    {
                        next_paths[next_path] = new List<Tuple<string, Type>>();
                    }
                    next_paths[next_path].Add(node);
                }
            }
            foreach (var next in next_paths)
            {
                tree.Add(new SearchTreeGroupEntry(new GUIContent(next.Key)) { level = level + 1 });
                BuildTree(level + 1, next.Value, tree);
            }

            foreach (var node in this_level_nodes)
            {
                tree.Add(new SearchTreeEntry(new GUIContent(ReflectionUtil.GetLastNameOfType(node))) { level = level + 1, userData = node });
            }
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Create Node"), 0),
            };

            var baseType = typeof(BaseRenderPass);
            var types = Assembly.GetAssembly(baseType).GetTypes();
            //types = Assembly.LoadFile(Application.dataPath + "/../Library/ScriptAssemblies/Assembly-CSharp.dll").GetTypes();
            types = types.Concat(Assembly.LoadFile(Application.dataPath + "/../Library/ScriptAssemblies/Assembly-CSharp.dll").GetTypes()).ToArray();
            List<Tuple<string, Type>> nodes = new List<Tuple<string, Type>>();
            foreach (var t in types)
            {
                if (ReflectionUtil.IsBasedRenderNode(t))
                {
                    var pathattris = t.GetCustomAttributes<RenderNodePathAttribute>();
                    foreach (var pathattri in pathattris)
                    {
                        if (!pathattri.hidden)
                            nodes.Add(new Tuple<string, Type>(pathattri.path, t));
                    }
                }
            }

            BuildTree(0, nodes, tree);

            return tree;
        }



        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            //try
            {
                Type type = (Type)entry.userData;
                var windowRoot = m_editorWindow.rootVisualElement;
                var windowMousePosition = windowRoot.ChangeCoordinatesTo(windowRoot.parent, context.screenMousePosition - m_editorWindow.position.position);
                var graphMousePosition = m_graphView.contentViewContainer.WorldToLocal(windowMousePosition);
                m_graphView.AddNodeFromTemplate(type, new Rect(graphMousePosition, Vector2.zero));
                return true;
            }
            //catch (Exception)
            {
                //Debug.LogError("Error occured when create node.");
                //return false;
            }
        }
    }
}
