#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace HypnosRenderPipeline
{
    public class HRPAtmo : ScriptableObject
    {
        [Min(10000)]
        public float planetRadius = 6371e3f;
        [Min(100)]
        public float AtmosphereThickness = 8e3f;

        public Color GroundAlbedo = new Color(0.25f, 0.25f, 0.25f);
        public Color RayleighScatter = new Color(0.1752f, 0.40785f, 1f);
        [Min(0)]
        public float RayleighScatterStrength = 1;
        [Min(0)]
        public float MieScatterStrength = 1;
        [Min(0)]
        public float OzoneStrength = 1;
        [Range(0.0001f, 0.03f)]
        public float sunSolidAngle = (0.5f / 180.0f * Mathf.PI);
        [Min(0)]
        public float MultiScatterStrength = 1f;

#if UNITY_EDITOR
        static public HRPAtmo Create()
        {
            var openFileName = new FileUtil.OpenFileName();
            openFileName.structSize = System.Runtime.InteropServices.Marshal.SizeOf(openFileName);
            openFileName.templateName = "*.asset";
            openFileName.filter = "HRP Atmo Data(*.asset)\0*.asset";
            openFileName.file = new string(new char[256]);
            openFileName.maxFile = openFileName.file.Length;
            openFileName.fileTitle = new string(new char[64]);
            openFileName.maxFileTitle = openFileName.fileTitle.Length;
            openFileName.initialDir = UnityEngine.Application.dataPath.Replace('/', '\\');
            openFileName.title = "Create HRP Atmo Data";
            openFileName.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008 | 0x00000002;
            if (FileUtil.GetSaveFileName(openFileName))
            {
                string path = openFileName.file.Substring(openFileName.file.IndexOf("Assets"));
                path = path.Replace('\\', '/');
                if (!path.Contains(".asset"))
                {
                    path += ".asset";
                }
                var old_asset = UnityEditor.AssetDatabase.LoadAssetAtPath(path, typeof(HRPAtmo));
                if (old_asset != null)
                {
                    UnityEditor.AssetDatabase.DeleteAsset(path);
                }
                var obj = ScriptableObject.CreateInstance<HRPAtmo>();
                UnityEditor.AssetDatabase.CreateAsset(obj, path);
                return obj;
            }
            return null;
        }

        [MenuItem("HypnosRenderPipeline/Atmo/Create Atmo Preset")]
        public static void CreateMenu()
        {
            Create();
        }
#endif
    }
}
