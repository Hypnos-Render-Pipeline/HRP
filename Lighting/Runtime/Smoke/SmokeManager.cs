using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;
using HypnosRenderPipeline.Tools;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace HypnosRenderPipeline
{
    public struct SmokeInfo
    {
        public float2 scale;
        public int width;
        public int index;
        public float g;
        public float scatter;
        public float absorb;
        public float maxDensity;
    };


    public sealed class SmokeManager
    {
        private class Nested { static Nested() { } internal static readonly SmokeManager instance = new SmokeManager(); }
        private static SmokeManager instance { get { return Nested.instance; } }

        HashSet<Material> matsSet = new HashSet<Material>();
        HashSet<Material> matsList = new HashSet<Material>();
        Dictionary<Material, float2> tex_scale = new Dictionary<Material, float2>();
        List<SmokeInfo> matInfo = new List<SmokeInfo>();

        bool regenerateAtlas = false;


        MaterialWithName atlasBlit = new MaterialWithName("Hidden/AtlasBlit");

        ComputeBuffer smoke_buffer;
        RenderTexture smoke_array;

        #region Pubic Methods

        public static void Regenerate() => instance.__Regenerate__();

        public static void GetSmokeAtlas(out ComputeBuffer buffer, out RenderTexture texture) => instance.__GetSmokeAtlas__(out buffer, out texture);

        static public void ReleaseResources() => instance.__ReleaseResources__();

        #endregion

        #region Private Methods

        private SmokeManager()
        {
#if UNITY_EDITOR
            CompilationPipeline.assemblyCompilationStarted += AssemblyCompilationStartedCallback;
#endif
        }

#if UNITY_EDITOR
        private void AssemblyCompilationStartedCallback(string assemb)
        {
            __ReleaseResources__();
        }
#endif

        void __ReleaseResources__()
        {
            if (smoke_buffer != null) smoke_buffer.Dispose();
            if (smoke_array != null) smoke_array.Release();
            regenerateAtlas = true;
        }

        void __Regenerate__() {
            regenerateAtlas = true;
        }

        void __GetSmokeAtlas__(out ComputeBuffer buffer, out RenderTexture texture)
        {
            var smokes = GameObject.FindObjectsOfType<HRPSmoke>();

            matsList.Clear();

            foreach (var smoke in smokes)
            {
                var mat = smoke.meshRenderer.sharedMaterial;
                //var tex = mat.GetTexture("_Volume");
                //if (tex != null)
                matsList.Add(mat);
            }

            regenerateAtlas |= !IsSame();
            {
                matsSet.Clear();
                foreach (var mat in matsList)
                {
                    matsSet.Add(mat);
                }
            }

            if (matsSet.Count > 0 && regenerateAtlas)
            {
                Dictionary<Texture2D, List<Material>> texs = new Dictionary<Texture2D, List<Material>>();
                foreach (var mat in matsSet)
                {
                    var tex = mat.GetTexture("_Volume") as Texture2D;
                    if (tex == null) tex = Texture2D.whiteTexture;
                    if (!texs.ContainsKey(tex)) texs.Add(tex, new List<Material>());
                    texs[tex].Add(mat);
                }

                int2 max_size = 0;
                foreach (var tex in texs)
                {
                    max_size = math.max(max_size, math.int2(tex.Key.width, tex.Key.height));
                }
                if (smoke_array != null) smoke_array.Release();
                RenderTextureDescriptor desc = new RenderTextureDescriptor(max_size.x, max_size.y, RenderTextureFormat.R16, 0, 0);
                desc.dimension = TextureDimension.Tex2DArray;
                desc.volumeDepth = texs.Count;
                desc.sRGB = false;
                smoke_array = new RenderTexture(desc);

                int material_index = 0;
                int tex_index = 0;
                matInfo.Clear();
                if (smoke_buffer != null) smoke_buffer.Dispose();
                smoke_buffer = new ComputeBuffer(matsSet.Count, Marshal.SizeOf<SmokeInfo>());
                tex_scale.Clear();
                CommandBuffer cb = new CommandBuffer();
                foreach (var tex in texs)
                {
                    var t = tex.Key;
                    float maxDensity = 0;
                    var wh = math.int2(t.width, t.height);
                    if (t == Texture2D.whiteTexture)
                    {
                        maxDensity = 1;
                        wh = max_size;
                    }
                    else
                    {
                        bool readable = t.isReadable;
                        if (!readable)
                        {
                            SetTextureImporterFormat(t, true);
                        }

                        var pixs = t.GetPixels();
                        foreach (var pix in pixs)
                        {
                            maxDensity = math.max(pix.r, maxDensity);
                        }

                        if (!readable)
                        {
                            SetTextureImporterFormat(t, false);
                        }
                    }

                    float2 scale = math.float2(wh) / max_size;

                    cb.SetGlobalVector("_Scale_Offset", math.float4(scale, 0, 0));
                    cb.SetRenderTarget(smoke_array, 0, CubemapFace.Unknown, tex_index);
                    cb.ClearRenderTarget(false, true, Color.clear);
                    cb.Blit(t, new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive), atlasBlit);

                    foreach (var mat in tex.Value)
                    {
                        mat.SetFloat("_MaterialID", material_index++);
                        tex_scale.Add(mat, scale);
                        matInfo.Add(new SmokeInfo() { index = tex_index, maxDensity = maxDensity });
                    }
                    tex_index++;
                }
                Graphics.ExecuteCommandBuffer(cb);
                regenerateAtlas = false;
            }

            if (smoke_buffer != null)
            {
                foreach (var mat in matsSet)
                {
                    int material_index = (int)mat.GetFloat("_MaterialID");
                    var info = matInfo[material_index];
                    int z = mat.GetInt("_SliceNum");
                    var width = mat.GetInt("_AtlasWidthCount");
                    var h = z / width + (z % width != 0 ? 1 : 0);
                    info.scale = tex_scale[mat] / math.float2(width, h);
                    float density = mat.GetFloat("_Density");
                    float scatter = mat.GetFloat("_Scatter");
                    info.scatter = density * scatter;
                    info.absorb = density * (1 - scatter);
                    info.width = (z << 16) + (width & 0xFFFF);
                    info.g = mat.GetFloat("_G") * 0.9f;
                    matInfo[material_index] = info;
                }

                smoke_buffer.SetData(matInfo.ToArray(), 0,0, matInfo.Count);
            }

            buffer = smoke_buffer;
            texture = smoke_array;

        }

        bool IsSame()
        {

            if (matsList.Count != matsSet.Count)
            {
                return false;
            }
            else
            {
                foreach (var mat in matsList)
                {
                    if (!matsSet.Contains(mat))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static void SetTextureImporterFormat(Texture2D texture, bool isReadable)
        {
#if UNITY_EDITOR
            if (null == texture) return;

            string assetPath = AssetDatabase.GetAssetPath(texture);
            var tImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (tImporter != null)
            {
                tImporter.textureType = TextureImporterType.Default;

                tImporter.isReadable = isReadable;

                AssetDatabase.ImportAsset(assetPath);
                AssetDatabase.Refresh();
            }
#endif
        }

#endregion
    }
}
