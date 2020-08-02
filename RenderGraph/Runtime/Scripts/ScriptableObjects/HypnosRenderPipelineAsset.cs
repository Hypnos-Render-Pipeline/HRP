using HypnosRenderPipeline.RenderGraph;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline
{
    public class HypnosRenderPipelineAsset : RenderPipelineAsset
    {
        public RenderGraphInfo hypnosRenderPipelineGraph;

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

            OpenFileName openFileName = new OpenFileName();
            openFileName.structSize = System.Runtime.InteropServices.Marshal.SizeOf(openFileName);
            openFileName.templateName = "*.asset";
            openFileName.filter = "HypnosRenderPipelineAsset(*.asset)\0*.asset";
            openFileName.file = new string(new char[256]);
            openFileName.maxFile = openFileName.file.Length;
            openFileName.fileTitle = new string(new char[64]);
            openFileName.maxFileTitle = openFileName.fileTitle.Length;
            openFileName.initialDir = UnityEngine.Application.dataPath.Replace('/', '\\');
            openFileName.title = "Save HRP";
            openFileName.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008 | 0x00000002;
            if (GetSaveFileName(openFileName))
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
                UnityEditor.AssetDatabase.CreateAsset(UnityEngine.ScriptableObject.CreateInstance<HypnosRenderPipelineAsset>(), path);
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public class OpenFileName
        {
            public int structSize = 0;
            public System.IntPtr dlgOwner = System.IntPtr.Zero;
            public System.IntPtr instance = System.IntPtr.Zero;
            public System.String filter = null;
            public System.String customFilter = null;
            public int maxCustFilter = 0;
            public int filterIndex = 0;
            public System.String file = null;
            public int maxFile = 0;
            public System.String fileTitle = null;
            public int maxFileTitle = 0;
            public System.String initialDir = null;
            public System.String title = null;
            public int flags = 0;
            public short fileOffset = 0;
            public short fileExtension = 0;
            public System.String defExt = null;
            public System.IntPtr custData = System.IntPtr.Zero;
            public System.IntPtr hook = System.IntPtr.Zero;
            public System.String templateName = null;
            public System.IntPtr reservedPtr = System.IntPtr.Zero;
            public int reservedInt = 0;
            public int flagsEx = 0;
        }

        [System.Runtime.InteropServices.DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern bool GetOpenFileName([System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] OpenFileName ofn);
        public static bool GetOFN([System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] OpenFileName ofn)
        {
            return GetOpenFileName(ofn);
        }

        [System.Runtime.InteropServices.DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern bool GetSaveFileName([System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] OpenFileName ofn);
        public static bool GetSFN([System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] OpenFileName ofn)
        {
            return GetSaveFileName(ofn);
        }
#endif
    }
}