using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace HypnosRenderPipeline.RenderGraph
{
    internal class RenderGraphViewWindow : EditorWindow
    {
        RenderGraphEditorView m_view;
        [MenuItem("HypnosRenderPipeline/Pipeline Graph/Editor")]
        public static RenderGraphViewWindow Create()
        {
            var window = GetWindow<RenderGraphViewWindow>();
            window.minSize = new Vector2(800, 500);
            window.name = "RenderGraph";
            window.titleContent = new GUIContent("RenderGraphEditor");
            return window;
        }


        [MenuItem("HypnosRenderPipeline/Pipeline Graph/Set Graph")]
        public static void Exe()
        {
            FileUtil.OpenFileName openFileName = new FileUtil.OpenFileName();
            openFileName.structSize = Marshal.SizeOf(openFileName);
            openFileName.templateName = "*.asset";
            openFileName.filter = "HRG(*.asset)\0*.asset";
            openFileName.file = new string(new char[256]);
            openFileName.maxFile = openFileName.file.Length;
            openFileName.fileTitle = new string(new char[64]);
            openFileName.maxFileTitle = openFileName.fileTitle.Length;
            openFileName.initialDir = Application.dataPath.Replace('/', '\\');
            openFileName.title = "Set HRG";
            openFileName.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008;

            if (FileUtil.GetOpenFileName(openFileName))
            {
                string path = openFileName.file.Substring(openFileName.file.IndexOf("Assets"));
                path = path.Replace('\\', '/');
                if (!path.Contains(".asset"))
                {
                    path += ".asset";
                }
                if (AssetDatabase.LoadAssetAtPath<RenderGraphInfo>(path) != null)
                {
                    AssetDatabase.LoadAssetAtPath<RenderGraphInfo>(path).TestExecute();
                }
            }
        }


        private void OnEnable()
        {
            m_view = new RenderGraphEditorView(this);
            rootVisualElement.Clear();
            rootVisualElement.Add(m_view);
            this.Focus();
        }

        private void OnDisable()
        {
            if (m_view != null) m_view.AutoSave();
        }

        public void Load(string path)
        {
            if (m_view!=null) m_view.Load(path);
        }
    }
}