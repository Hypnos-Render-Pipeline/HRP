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
            HypnosRenderPipeline.FileUtil.SaveAssetInProject<HRPTerrainData>();
        }
#endif
    }
}
