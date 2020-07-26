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
            RenderGraphSaveFileWindow.OpenFileName openFileName = new RenderGraphSaveFileWindow.OpenFileName();
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


    internal class RenderGraphSaveFileWindow
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class OpenFileName
        {
            public int structSize = 0;
            public IntPtr dlgOwner = IntPtr.Zero;
            public IntPtr instance = IntPtr.Zero;
            public String filter = null;
            public String customFilter = null;
            public int maxCustFilter = 0;
            public int filterIndex = 0;
            public String file = null;
            public int maxFile = 0;
            public String fileTitle = null;
            public int maxFileTitle = 0;
            public String initialDir = null;
            public String title = null;
            public int flags = 0;
            public short fileOffset = 0;
            public short fileExtension = 0;
            public String defExt = null;
            public IntPtr custData = IntPtr.Zero;
            public IntPtr hook = IntPtr.Zero;
            public String templateName = null;
            public IntPtr reservedPtr = IntPtr.Zero;
            public int reservedInt = 0;
            public int flagsEx = 0;
        }

        [DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
        public static extern bool GetOpenFileName([In, Out] OpenFileName ofn);
        public static bool GetOFN([In, Out] OpenFileName ofn)
        {
            return GetOpenFileName(ofn);
        }

        [DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
        public static extern bool GetSaveFileName([In, Out] OpenFileName ofn);
        public static bool GetSFN([In, Out] OpenFileName ofn)
        {
            return GetSaveFileName(ofn);
        }
    }
}