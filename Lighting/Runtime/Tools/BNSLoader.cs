using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline
{
    public class BNSLoader : IDisposable
    {
        private class Nested { static Nested() { } internal static readonly BNSLoader instance = new BNSLoader(); }
        public static BNSLoader instance { get { return Nested.instance; } }

        RenderTexture tex_rankingTile_, tex_scrambling_, tex_sobol_;
        public RenderTexture tex_rankingTile { get { if (tex_rankingTile_ == null) Load(); return tex_rankingTile_; } }
        public RenderTexture tex_scrambling { get { if (tex_scrambling_ == null) Load(); return tex_scrambling_; } }
        public RenderTexture tex_sobol { get { if (tex_sobol_ == null) Load(); return tex_sobol_; } }
        ComputeBuffer dataBuffer0, dataBuffer1, dataBuffer2;
        Material writeMaterial;

        private BNSLoader()
        {
            Load();
        }

        private void Load(CommandBuffer cb)
        {
            tex_rankingTile_ = new RenderTexture(Resources.Load<RenderTexture>("Textures/Random Lut/rankingTile"));
            tex_scrambling_ = new RenderTexture(Resources.Load<RenderTexture>("Textures/Random Lut/scramblingTile"));
            tex_sobol_ = new RenderTexture(Resources.Load<RenderTexture>("Textures/Random Lut/sobol_256spp_256d"));
            writeMaterial = new Material(Shader.Find("Hidden/WriteTexture"));
            {
                var rankingTile = Resources.Load<TextAsset>("Textures/Random Lut/rankingTile");
                string str = rankingTile.text;
                var datas = str.Split(',');
                List<int> data_int = new List<int>();
                foreach (var data in datas)
                {
                    data_int.Add(int.Parse(data));
                }
                dataBuffer0 = new ComputeBuffer(data_int.Count, 4);
                dataBuffer0.SetData(data_int);
                cb.SetGlobalBuffer("_Data", dataBuffer0);
                for (int i = 0; i < 8; i++)
                {
                    cb.SetRenderTarget(tex_rankingTile_, 0, CubemapFace.Unknown, i);
                    cb.SetGlobalInt("_Slice", i);
                    cb.Blit(null, new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive), writeMaterial, 1);
                }
            }
            {
                var sobol_256spp_256d = Resources.Load<TextAsset>("Textures/Random Lut/sobol_256spp_256d");
                string str = sobol_256spp_256d.text;
                var datas = str.Split(',');
                List<int> data_int = new List<int>();
                foreach (var data in datas)
                {
                    data_int.Add(int.Parse(data));
                }
                dataBuffer1 = new ComputeBuffer(data_int.Count, 4);
                dataBuffer1.SetData(data_int);
                cb.SetGlobalBuffer("_Data", dataBuffer1);
                cb.Blit(null, tex_sobol_, writeMaterial, 0);
            }
            {
                var scramblingTile = Resources.Load<TextAsset>("Textures/Random Lut/scramblingTile");
                string str = scramblingTile.text;
                var datas = str.Split(',');
                List<int> data_int = new List<int>();
                foreach (var data in datas)
                {
                    data_int.Add(int.Parse(data));
                }
                dataBuffer2 = new ComputeBuffer(data_int.Count, 4);
                dataBuffer2.SetData(data_int);
                cb.SetGlobalBuffer("_Data", dataBuffer2);
                for (int i = 0; i < 8; i++)
                {
                    cb.SetRenderTarget(tex_scrambling_, 0, CubemapFace.Unknown, i);
                    cb.SetGlobalInt("_Slice", i);
                    cb.Blit(null, new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive), writeMaterial, 1);
                }
            }
        }

        public void Load()
        {
            CommandBuffer cb = new CommandBuffer();
            Load(cb);
            Graphics.ExecuteCommandBuffer(cb);
            Dispose();
        }

        public void Dispose()
        {
            if (dataBuffer0 != null) dataBuffer0.Dispose();
            if (dataBuffer1 != null) dataBuffer1.Dispose();
            if (dataBuffer2 != null) dataBuffer2.Dispose();
        }
    }
}