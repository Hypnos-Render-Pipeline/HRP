using HypnosRenderPipeline.RenderGraph;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline.RenderGraph
{
    public class HypnosRenderPipelineAsset : RenderPipelineAsset
    {

#if UNITY_EDITOR
        public bool useCompliedCodeInEditor = false;
#endif

        public HypnosRenderGraph hypnosRenderPipelineGraph;

        public HRPMaterialResources materialResources;

        private Shader m_defaultShader = null;
        public override Shader defaultShader { get { if (m_defaultShader == null) m_defaultShader = Shader.Find("HRP/Lit"); return m_defaultShader; } }

        public override Material defaultMaterial { get { return materialResources.defaultMaterial; } }

        protected override RenderPipeline CreatePipeline()
        {
            var ls = GameObject.FindObjectsOfType<Light>();
            foreach (var l in ls)
            {
                if (l.GetComponent<HRPLight>() == null)
                {
                    HRPLight.GenerateHRPLight(l);
                }
            }
            return new HypnosRenderPipeline(this);
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("HypnosRenderPipeline/PipelineAsset")]
        static void CreateAsset()
        {

            var openFileName = new FileUtil.OpenFileName();
            openFileName.structSize = System.Runtime.InteropServices.Marshal.SizeOf(openFileName);
            openFileName.templateName = "*.asset";
            openFileName.filter = "HypnosRenderPipelineAsset(*.asset)\0*.asset";
            openFileName.file = new string(new char[256]);
            openFileName.maxFile = openFileName.file.Length;
            openFileName.fileTitle = new string(new char[64]);
            openFileName.maxFileTitle = openFileName.fileTitle.Length;
            openFileName.initialDir = UnityEngine.Application.dataPath.Replace('/', '\\');
            openFileName.title = "Create HRP Asset";
            openFileName.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008 | 0x00000002;
            if (FileUtil.GetSaveFileName(openFileName))
            {
                string path = openFileName.file.Substring(openFileName.file.IndexOf("Assets"));
                path = path.Replace('\\', '/');
                if (!path.Contains(".asset"))
                {
                    path += ".asset";
                }
                var old_asset = UnityEditor.AssetDatabase.LoadAssetAtPath(path, typeof(HypnosRenderPipelineAsset));
                if (old_asset != null)
                {
                    UnityEditor.AssetDatabase.DeleteAsset(path);
                }
                var obj = UnityEngine.ScriptableObject.CreateInstance<HypnosRenderPipelineAsset>();
                obj.materialResources = AssetDatabase.LoadAssetAtPath<HRPMaterialResources>(PathDefine.path + "Lighting/Runtime/Resources/HRPDefaultResources.asset");
                UnityEditor.AssetDatabase.CreateAsset(obj, path);
            }
        }

#endif
    }
}