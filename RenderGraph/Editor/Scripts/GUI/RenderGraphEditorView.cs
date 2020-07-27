using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace HypnosRenderPipeline.RenderGraph
{

    internal class RenderGraphEditorView : VisualElement
    {
        RenderGraphInfo m_renderGraphInfo;

        EditorWindow m_editorWindow;
        RenderGraphView m_graphView;

        HRGEditorData __editorData__;
        HRGEditorData m_editorData { get { if (__editorData__ == null) __editorData__ = AssetDatabase.LoadAssetAtPath<HRGEditorData>("Assets/HRP/RenderGraph/Editor/EditorData.asset"); return __editorData__; } }

        string assetName = "unnamed";

        public RenderGraphEditorView(EditorWindow editorWindow)
        {
            m_editorWindow = editorWindow;
            m_renderGraphInfo = ScriptableObject.CreateInstance<RenderGraphInfo>();
            Undo.undoRedoPerformed += UndoRedoCallback;

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

                GUILayout.Space(6);

                if (GUILayout.Button("Test Execute", EditorStyles.toolbarButton))
                {
                    m_renderGraphInfo.TestExecute();
                }

                GUILayout.Space(26);

                GUILayout.Label(assetName);

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

                m_graphView.SetupZoom(0.05f, ContentZoomer.DefaultMaxScale * 4);
                m_graphView.AddManipulator(new ContentDragger());
                m_graphView.AddManipulator(new SelectionDragger());
                m_graphView.AddManipulator(new RectangleSelector());
                m_graphView.AddManipulator(new ClickSelector());
                content.Add(m_graphView);

                m_graphView.SetGraphInfo(m_renderGraphInfo);
            }

            Add(content);

            StyleEnum<ScaleMode> a = new StyleEnum<ScaleMode>();
            a.value = ScaleMode.ScaleAndCrop;
            m_graphView.style.unityBackgroundScaleMode = a;
            m_graphView.style.unityBackgroundImageTintColor = new StyleColor(new Color(0.7f, 0.7f, 0.8f));

            AutoLoad();
        }

        void UndoRedoCallback()
        {
        }

        public void New()
        {
            if (assetName != "unnamed") Save();
            m_renderGraphInfo = ScriptableObject.CreateInstance<RenderGraphInfo>();
            m_graphView.SetGraphInfo(m_renderGraphInfo);
            assetName = "unnamed";
        }

        public void AutoSave()
        {
            if (assetName != "unnamed")
            {
                Save();
                m_editorData.lastOpenPath = assetName;
                EditorUtility.SetDirty(m_editorData);
                AssetDatabase.Refresh();
            }
            m_renderGraphInfo = ScriptableObject.CreateInstance<RenderGraphInfo>();
            m_graphView.SetGraphInfo(m_renderGraphInfo);
            assetName = "unnamed";
        }
         
        public void AutoLoad()
        {
            if (m_editorData.lastOpenPath != "unnamed")
            {
                try
                {
                    Load(m_editorData.lastOpenPath);
                }
                catch (System.Exception)
                {
                    New();
                    return;
                }
                m_renderGraphInfo.OnAfterDeserialize(); // beacuse this function call happend on code hot recompile, so we need to Deserialize and delete error edges & nodes.
                Load(m_editorData.lastOpenPath);
            }
        }

        public void Load(string path)
        {
            if (assetName != "unnamed") Save();
            RenderGraphInfo info;
            try
            {
                info = AssetDatabase.LoadAssetAtPath<RenderGraphInfo>(path);
                if (info == null) throw new System.Exception("Null");
            }
            catch
            {
                Debug.LogError(string.Format("Load Render Graph Info at \"{0}\" faild!", path));
                return;
            }
            m_renderGraphInfo = info;
            m_graphView.SetGraphInfo(info);

            assetName = path;

            m_graphView.contentViewContainer.transform.position = m_renderGraphInfo.viewPosition;
            m_graphView.contentViewContainer.transform.scale = m_renderGraphInfo.viewScale;

            m_renderGraphInfo.TestExecute();
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

        public void Save()
        {
            m_renderGraphInfo.viewPosition = m_graphView.contentViewContainer.transform.position;
            m_renderGraphInfo.viewScale = m_graphView.contentViewContainer.transform.scale;
            if (AssetDatabase.Contains(m_renderGraphInfo))
            {
                EditorUtility.SetDirty(m_renderGraphInfo);
                AssetDatabase.SaveAssets();
                assetName = AssetDatabase.GetAssetPath(m_renderGraphInfo);
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
                    assetName = path;
                }
            }
        }
    }
}