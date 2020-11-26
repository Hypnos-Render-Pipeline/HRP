using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;
using UnityEngine.Rendering;
using HypnosRenderPipeline.Tools;


namespace HypnosRenderPipeline
{

    [ExecuteInEditMode]
    public class HRPTerrain : MonoBehaviour
    {
        [Space(5)]
        [Header("Mesh used for Render")]

        public int tileCount = 16;

        public float tileSize = 16;

        [Range(2, 8)]
        public int maxLodGridCount = 4;

        [Range(1, 12)]
        public int lodNum = 4;

        public AnimationCurve lodCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Space(5)]
        [Header("Terrain Data")]
        public HRPTerrainData terrainData;

        int maxTileVertexCount = 0;

        Mesh[] lodMeshs;

        MaterialWithName mat = new MaterialWithName("HRP/Terrain");

        Vector2[][] instances;
        System.Collections.Generic.List<Vector2>[] instancesList;
        MaterialPropertyBlock[] properties;
        IndirectCullingBuffers[] buffers;

        class IndirectCullingBuffers
        {
            public ComputeBuffer argsBuffer;
            public ComputeBuffer indexBuffer;
            public ComputeBuffer offsetBuffer;

            public void Release()
            {
                if (argsBuffer != null) argsBuffer.Release();
                if (indexBuffer != null) indexBuffer.Release();
                if (offsetBuffer != null) offsetBuffer.Release();
                argsBuffer = null;
                indexBuffer = null;
                offsetBuffer = null;
            }
        }

        public CommandBuffer cb;

        [HideInInspector]
        public Texture2D lodTexture;

        void OnEnable()
        {
            Generate();
        }

        void OnDisable()
        {
            Destroy();
        }

        public void MoveTerrain(CommandBuffer commandBuffer, Camera cam)
        {
            var terrain_center = new Vector4(cam.transform.position.x, 0, cam.transform.position.z);
            float min_quad_size = tileSize / maxTileVertexCount * 4;
            terrain_center = floor(terrain_center / min_quad_size) * min_quad_size;
            float k = tileSize * tileCount / -2;
            commandBuffer.SetGlobalVector("_TileLB_tileSize", new Vector4(k + terrain_center.x, k + terrain_center.z, tileSize));
            commandBuffer.SetGlobalVector("_TerrainCenter", terrain_center);
            commandBuffer.SetGlobalVector("_LocalToWorld", terrain_center);
        }

        public void Destroy()
        {
            if (buffers != null)
            {
                for (int i = 0; i < buffers.Length; i++)
                {
                    buffers[i].Release();
                }
                buffers = null;
            }
            cb = null;
        }

        public void Generate()
        {
            Destroy();

            cb = new CommandBuffer();
            cb.name = "Terrain";

            cb.Clear();

            properties = new MaterialPropertyBlock[lodNum];
            for (int i = 0; i < lodNum; i++)
            {
                properties[i] = new MaterialPropertyBlock();
            }

            maxLodGridCount = maxLodGridCount / 2 * 2;
            maxTileVertexCount = maxLodGridCount * (1 << (lodNum - 1));
            if (maxTileVertexCount >= 256) Debug.LogWarning("Tile vertex num over limit!");

            GenerateLodMeshs();

            lodTexture = new Texture2D(tileCount, tileCount, TextureFormat.ARGB4444, false);

            mat.material.SetTexture("_LodTex", lodTexture);

            CalculateLod();

            instances = new Vector2[lodNum][];
            for (int i = 0; i < lodNum; i++)
                instances[i] = instancesList[i].ToArray();


            buffers = new IndirectCullingBuffers[lodNum];

            cb.Clear();
            cb.SetGlobalFloat("_PlaneSize", tileSize);
            for (int i = 0; i < lodNum; i++)
            {
                var cullingBuffers = new IndirectCullingBuffers();
                cullingBuffers.offsetBuffer = new ComputeBuffer(max(instances[i].Length, 1), 8);
                cullingBuffers.offsetBuffer.SetData(instances[i]);
                cullingBuffers.argsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
                cullingBuffers.argsBuffer.SetData(new[] { lodMeshs[i].GetIndexCount(0), 0u, 0u, 0u, 0u });
                cullingBuffers.indexBuffer = new ComputeBuffer(max(instances[i].Length, 1), 4);
                buffers[i] = cullingBuffers;

                if (instances[i].Length == 0) continue;

                FrustumCulling.CullPlanes(cb, cullingBuffers.argsBuffer, cullingBuffers.offsetBuffer, cullingBuffers.indexBuffer);

                properties[i].SetBuffer("_TilePos", cullingBuffers.offsetBuffer);
                properties[i].SetBuffer("_TileIndex", cullingBuffers.indexBuffer);
                cb.DrawMeshInstancedIndirect(lodMeshs[i], 0, mat, i == 0 ? 0 : 1, cullingBuffers.argsBuffer, 0, properties[i]);
            }
        }


        void CalculateLod()
        {
            float[,] lods = new float[tileCount + 1, tileCount + 1];
            Color[] lodColor = new Color[tileCount * tileCount];
            int[,] terrainTiles = new int[tileCount, tileCount];

            instancesList = new System.Collections.Generic.List<Vector2>[lodNum];
            for (int i = 0; i < lodNum; i++)
            {
                instancesList[i] = new System.Collections.Generic.List<Vector2>();
            }

            for (int i = 0; i < tileCount + 1; i++)
                for (int j = 0; j < tileCount + 1; j++)
                    lods[i, j] = lodNum;

            for (int i = 0; i < tileCount; i++)
            {
                for (int j = 0; j < tileCount; j++)
                {
                    var lbpos = GetTileLBPosition(i, j);
                    var rbpos = lbpos + Vector3.right * tileSize;
                    var rupos = rbpos + Vector3.forward * tileSize;
                    var lupos = lbpos + Vector3.forward * tileSize;

                    var conerLods = float4(CalculateLod(lbpos), CalculateLod(rbpos), CalculateLod(lupos), CalculateLod(rupos));
                    var min_lod = floor(Mathf.Min(conerLods.x, conerLods.y, conerLods.z, conerLods.w));
                    conerLods = clamp(conerLods, min_lod, min_lod + 1);
                    lods[i, j] = min(lods[i, j], conerLods.x);
                    lods[i + 1, j] = min(lods[i + 1, j], conerLods.y);
                    lods[i, j + 1] = min(lods[i, j + 1], conerLods.z);
                    lods[i + 1, j + 1] = min(lods[i + 1, j + 1], conerLods.w);
                }
            }

            System.Collections.Generic.HashSet<int2> overflows = new System.Collections.Generic.HashSet<int2>();
            for (int i = 0; i < tileCount; i++)
            {
                for (int j = 0; j < tileCount; j++)
                {
                    float4 cornerLod = float4(lods[i, j], lods[i + 1, j], lods[i, j + 1], lods[i + 1, j + 1]);
                    int minlod = (int)floor(Mathf.Min(cornerLod.x, cornerLod.y, cornerLod.z, cornerLod.w));
                    if (any(cornerLod - minlod > 1))
                    {
                        if (cornerLod.x - minlod > 1) overflows.Add(int2(i, j));
                        if (cornerLod.y - minlod > 1) overflows.Add(int2(i + 1, j));
                        if (cornerLod.z - minlod > 1) overflows.Add(int2(i, j + 1));
                        if (cornerLod.w - minlod > 1) overflows.Add(int2(i + 1, j + 1));
                    }

                    float4 lod_ = cornerLod - minlod;
                    Color color = new Color(lod_.x, lod_.y, lod_.z, lod_.w);
                    lodColor[i + j * tileCount] = color;
                    SetLod(i, j, minlod);
                    terrainTiles[i, j] = minlod;
                }
            }
            foreach (var over in overflows)
            {
                lods[over.x, over.y] -= 1;
            }

            for (int i = 0; i < tileCount; i++)
            {
                for (int j = 0; j < tileCount; j++)
                {
                    float4 cornerLod = float4(lods[i, j], lods[i + 1, j], lods[i, j + 1], lods[i + 1, j + 1]);
                    int minlod = terrainTiles[i, j];

                    float4 lod_ = cornerLod - minlod;
                    Color color = new Color(lod_.x, lod_.y, lod_.z, lod_.w);
                    lodColor[i + j * tileCount] = color;
                }
            }

            //for (int i = 0; i < tileCount; i++)
            //{
            //    for (int j = 0; j < tileCount; j++)
            //    {
            //        float4 cornerLod = float4(lods[i, j], lods[i + 1, j], lods[i, j + 1], lods[i + 1, j + 1]);
            //        int minlod = (int)floor(Mathf.Min(cornerLod.x, cornerLod.y, cornerLod.z, cornerLod.w));

            //        float4 lod_ = cornerLod - minlod;
            //        Color color = new Color(lod_.x, lod_.y, lod_.z, lod_.w);
            //    }
            //}
            lodTexture.SetPixels(lodColor);
            lodTexture.Apply();
            float k = tileSize * tileCount / -2;
        }

        float CalculateLod(Vector3 pos)
        {
            float max_dis = tileCount / 2 * tileSize;
            return math.saturate(lodCurve.Evaluate(pos.magnitude / max_dis)) * (lodNum - 1);
        }

        void SetLod(int x, int y, int lod)
        {
            lod = math.min(lod, lodNum - 1);
            var offset = GetTileLBPosition(x, y);
            instancesList[lod].Add(new Vector2(offset.x, offset.z));
        }

        Vector3 GetTileCenterPosition(int x, int y)
        {
            float k = tileSize * tileCount / 2;
            return math.float3(x + 0.5f, 0, y + 0.5f) * tileSize - math.float3(k, 0, k);
        }

        Vector3 GetTileLBPosition(int x, int y)
        {
            float k = tileSize * tileCount / 2;
            return float3(x, 0, y) * tileSize - float3(k, 0, k);
        }

        void GenerateLodMeshs()
        {
            lodMeshs = new Mesh[lodNum];

            for (int i = 0; i < lodNum; i++)
            {
                lodMeshs[i] = GenerateGridMesh(tileSize, (1 << (lodNum - i - 1)) * maxLodGridCount);
                lodMeshs[i].name = i.ToString();
            }
        }

        Mesh GenerateGridMesh(float size, int count)
        {
            Mesh mesh = new Mesh();

            float gridSize = size / count;

            int vertexLineCount = count + 1;
            int vertexTotalCount = vertexLineCount * vertexLineCount;

            Vector3[] verts = new Vector3[vertexTotalCount];
            Vector2[] uv = new Vector2[vertexTotalCount];
            Vector2[] uv2 = new Vector2[vertexTotalCount];
            for (int i = 0; i < vertexTotalCount; i++)
            {
                verts[i] = new Vector3((i % vertexLineCount), 0, i / vertexLineCount) * gridSize;
                uv[i] = new Vector2(math.max(i % vertexLineCount - 1, 0), math.max(i / vertexLineCount - 1, 0)) / (vertexLineCount - 2);

                int2 offset = math.int2(i % vertexLineCount % 4, i / vertexLineCount % 4);

                Vector2 offsetuv = Vector2.zero;

                if (distance(offset, 0) < sqrt(2.1)) offsetuv = -(float2)offset * gridSize;
                if (distance(offset, float2(0, 4)) < sqrt(2.1)) offsetuv = (float2(0, 4) - offset) * gridSize;
                if (distance(offset, float2(4, 4)) < sqrt(2.1)) offsetuv = (float2(4, 4) - offset) * gridSize;
                if (distance(offset, float2(4, 0)) < sqrt(2.1)) offsetuv = (float2(4, 0) - offset) * gridSize;
                if (distance(offset, float2(2, 2)) < sqrt(2.1)) offsetuv = (offset - float2(2, 2)) * gridSize;

                //if (offset.x == 2 && offset.y == 1) offsetuv = new Vector2(0, -gridSize);
                //if (offset.x == 1 && offset.y == 2) offsetuv = new Vector2(-gridSize, 0);
                //if (offset.x == 1 && offset.y == 0) offsetuv = new Vector2(-gridSize, 0);
                //else if (offset.x == 1 && offset.y == 1) offsetuv = new Vector2(-gridSize, -gridSize);
                //else if (offset.x == 0 && offset.y == 1) offsetuv = new Vector2(0, -gridSize);

                uv2[i] = offsetuv;
            }

            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.uv2 = uv2;

            int triTotalCount = count * count * 2;
            int[] tris = new int[triTotalCount * 3];
            for (int i = 0; i < triTotalCount; i++)
            {
                int grid_id = i / 2;
                int a, b, c;
                a = grid_id % count + grid_id / count * vertexLineCount;
                if ((grid_id % count % 2) + (grid_id / count % 2) == 1)
                {
                    if (i % 2 == 0)
                    {
                        b = a + vertexLineCount;
                        c = a + 1;
                    }
                    else
                    {
                        b = a + vertexLineCount;
                        a = a + 1;
                        c = a + vertexLineCount;
                    }
                }
                else
                {
                    if (i % 2 == 0)
                    {
                        b = a + 1 + vertexLineCount;
                        c = a + 1;
                    }
                    else
                    {
                        b = a + vertexLineCount;
                        c = b + 1;
                    }
                }
                tris[i * 3] = a;
                tris[i * 3 + 1] = b;
                tris[i * 3 + 2] = c;
            }
            mesh.triangles = tris;

            mesh.RecalculateNormals();

            return mesh;
        }

#if UNITY_EDITOR

        [UnityEditor.MenuItem("GameObject/3D Object/HRP Terrain")]
        static void CreateTerrain()
        {
            GameObject go = new GameObject();
            go.name = "Terrain";
            go.AddComponent<HRPTerrain>();
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create HRP Terrain");
        }

#endif
    }

}
