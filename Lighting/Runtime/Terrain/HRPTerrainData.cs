//using Unity.Mathematics;
//using static Unity.Mathematics.math;
using UnityEngine;

namespace HypnosRenderPipeline
{
    public class HRPTerrainData : ScriptableObject
    {
        public Vector2 center = Vector2.zero;

        public Vector2Int tileCount = new Vector2Int(16, 16);

        public Vector2 size { get { return ((Vector2)tileCount * tileSize); } }

        public float maxSize { get { return Mathf.Max(size.x, size.y); } }
        public Vector3 centerV3 { get { return new Vector3(center.x, 0, center.y); } }
        public Vector3 sizeV3 { get { return new Vector3(size.x, 1, size.y); } }

        public float tileSize = 128;

        public Vector2 heightRange = new Vector2(0, 500);

        public Texture2D[] height = new Texture2D[16 * 16];

#if UNITY_EDITOR

         [UnityEditor.MenuItem("HypnosRenderPipeline/Terrain/Terrain Data")]
        static void CreateTerrainData()
        {
            var openFileName = new FileUtil.OpenFileName();
            openFileName.structSize = System.Runtime.InteropServices.Marshal.SizeOf(openFileName);
            openFileName.templateName = "*.asset";
            openFileName.filter = "HRP Terrain Data(*.asset)\0*.asset";
            openFileName.file = new string(new char[256]);
            openFileName.maxFile = openFileName.file.Length;
            openFileName.fileTitle = new string(new char[64]);
            openFileName.maxFileTitle = openFileName.fileTitle.Length;
            openFileName.initialDir = UnityEngine.Application.dataPath.Replace('/', '\\');
            openFileName.title = "Create HRP Terrain Data";
            openFileName.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008 | 0x00000002;
            if (FileUtil.GetSaveFileName(openFileName))
            {
                string path = openFileName.file.Substring(openFileName.file.IndexOf("Assets"));
                path = path.Replace('\\', '/');
                if (!path.Contains(".asset"))
                {
                    path += ".asset";
                }
                var old_asset = UnityEditor.AssetDatabase.LoadAssetAtPath(path, typeof(HRPTerrainData));
                if (old_asset != null)
                {
                    UnityEditor.AssetDatabase.DeleteAsset(path);
                }
                var obj = ScriptableObject.CreateInstance<HRPTerrainData>();
                UnityEditor.AssetDatabase.CreateAsset(obj, path);
            }
        }
#endif
    }
}
