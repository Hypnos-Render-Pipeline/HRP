using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.Experimental.TerrainAPI;
using UnityEngine;

namespace HypnosRenderPipeline.RenderGraph
{
    internal class RenderGraphViewWindow : EditorWindow
    {
        RenderGraphEditorView m_view;
        [MenuItem("HypnosRenderPipeline/RenderGraph")]
        public static RenderGraphViewWindow Create()
        {
            var window = GetWindow<RenderGraphViewWindow>();
            window.minSize = new Vector2(800, 500);
            window.name = "RenderGraph";
            window.titleContent = new GUIContent("RenderGraph");
            window.OnEnable();
            return window;
        }

        private void OnEnable()
        {
            m_view = new RenderGraphEditorView(this);
            rootVisualElement.Clear();
            rootVisualElement.Add(m_view);
        }

        private void OnDisable()
        {
            m_view.AutoSave();
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