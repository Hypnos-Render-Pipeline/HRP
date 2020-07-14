using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace HypnosRenderPipeline.RenderGraph
{

    internal class RenderGraphEditorView : VisualElement
    {
        RenderGraphInfo m_renderGraphInfo;

        EditorWindow m_editorWindow;
        RenderGraphView m_graphView;
        public RenderGraphEditorView(EditorWindow editorWindow)
        {
            m_editorWindow = editorWindow;
            m_renderGraphInfo = ScriptableObject.CreateInstance<RenderGraphInfo>();

            StyleLoader.Load(this);

            var toolbar = new IMGUIContainer(() =>
            {
                GUILayout.BeginHorizontal(EditorStyles.toolbar);

                if (GUILayout.Button("New", EditorStyles.toolbarButton))
                {
                    New();
                }

                GUILayout.Space(6);

                if (GUILayout.Button("Load", EditorStyles.toolbarButton))
                {
                    Load();
                }

                GUILayout.Space(6);

                if (GUILayout.Button("Save", EditorStyles.toolbarButton))
                {
                    Save();
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            });
            Add(toolbar);

            var content = new VisualElement { name = "content" };
            {
                m_graphView = new RenderGraphView(this, m_editorWindow)
                {
                    name = "GraphView",
                    viewDataKey = "RenderGraphView"
                };

                m_graphView.SetupZoom(0.05f, ContentZoomer.DefaultMaxScale);
                m_graphView.AddManipulator(new ContentDragger());
                m_graphView.AddManipulator(new SelectionDragger());
                m_graphView.AddManipulator(new RectangleSelector());
                m_graphView.AddManipulator(new ClickSelector());
                m_graphView.RegisterCallback<KeyDownEvent>(KeyDown);
                content.Add(m_graphView);

                m_graphView.SetGraphInfo(m_renderGraphInfo);
            }

            Add(content);
        }
        
        void KeyDown(KeyDownEvent e)
        {
            //Debug.Log("KeyDown");
        }

        public void Load(string path)
        {
            RenderGraphInfo info;
            try
            {
                info = AssetDatabase.LoadAssetAtPath<RenderGraphInfo>(path);
            }
            catch
            {
                Debug.LogError(string.Format("Load Render Graph Info at \"{0}\" faild!", path));
                return;
            }
            m_renderGraphInfo = info;
            m_graphView.SetGraphInfo(info);
        }

        public void Load()
        {
            RenderGraphSaveFileWindow.OpenFileName openFileName = new RenderGraphSaveFileWindow.OpenFileName();
            openFileName.structSize = Marshal.SizeOf(openFileName);
            openFileName.templateName = "*.asset";
            openFileName.filter = "HRG(*.asset)\0*.asset";
            openFileName.file = new string(new char[256]);
            openFileName.maxFile = openFileName.file.Length;
            openFileName.fileTitle = new string(new char[64]);
            openFileName.maxFileTitle = openFileName.fileTitle.Length;
            openFileName.initialDir = Application.dataPath.Replace('/', '\\');
            openFileName.title = "Load HRG";
            openFileName.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008;

            if (RenderGraphSaveFileWindow.GetOpenFileName(openFileName))
            {
                string path = openFileName.file.Substring(openFileName.file.IndexOf("Assets"));
                path = path.Replace('\\', '/');
                if (!path.Contains(".asset"))
                {
                    path += ".asset";
                }
                if (AssetDatabase.LoadAssetAtPath<RenderGraphInfo>(path) != null)
                {
                    Load(path);
                }
            }
        }

        public void New()
        {
            m_renderGraphInfo = ScriptableObject.CreateInstance<RenderGraphInfo>();
            m_graphView.SetGraphInfo(m_renderGraphInfo);
        }

        public void Save()
        {
            Debug.Log("Saved");
            if (AssetDatabase.Contains(m_renderGraphInfo))
            {
                EditorUtility.SetDirty(m_renderGraphInfo);
                AssetDatabase.SaveAssets();
            }
            else
            {
                RenderGraphSaveFileWindow.OpenFileName openFileName = new RenderGraphSaveFileWindow.OpenFileName();
                openFileName.structSize = Marshal.SizeOf(openFileName);
                openFileName.templateName = "*.asset";
                openFileName.filter = "HRG(*.asset)\0*.asset";
                openFileName.file = new string(new char[256]);
                openFileName.maxFile = openFileName.file.Length;
                openFileName.fileTitle = new string(new char[64]);
                openFileName.maxFileTitle = openFileName.fileTitle.Length;
                openFileName.initialDir = Application.dataPath.Replace('/', '\\');
                openFileName.title = "Save HRG";
                openFileName.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008 | 0x00000002;

                if (RenderGraphSaveFileWindow.GetSaveFileName(openFileName))
                {
                    string path = openFileName.file.Substring(openFileName.file.IndexOf("Assets"));
                    path = path.Replace('\\', '/');
                    if (!path.Contains(".asset"))
                    {
                        path += ".asset";
                    }
                    var old_asset = AssetDatabase.LoadAssetAtPath(path, typeof(RenderGraphInfo));
                    if (old_asset != null)
                    {
                        AssetDatabase.DeleteAsset(path);
                    }
                    AssetDatabase.CreateAsset(m_renderGraphInfo, path);
                }
            }
        }
    }
}