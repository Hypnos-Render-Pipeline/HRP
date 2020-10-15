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

        readonly public RenderTexture tex_rankingTile, tex_scrambling, tex_sobol;
        ComputeBuffer dataBuffer0, dataBuffer1, dataBuffer2;
        Material writeMaterial;

        private BNSLoader()
        {
            tex_rankingTile = new RenderTexture(Resources.Load<RenderTexture>("Textures/Random Lut/rankingTile"));
            tex_scrambling = new RenderTexture(Resources.Load<RenderTexture>("Textures/Random Lut/scramblingTile"));
            tex_sobol = new RenderTexture(Resources.Load<RenderTexture>("Textures/Random Lut/sobol_256spp_256d"));
            writeMaterial = new Material(Shader.Find("Hidden/WriteTexture"));
            Load();
        }

        private void Load(CommandBuffer cb)
        {
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
                    cb.SetRenderTarget(tex_rankingTile, 0, CubemapFace.Unknown, i);
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
                cb.Blit(null, tex_sobol, writeMaterial, 0);
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
                    cb.SetRenderTarget(tex_scrambling, 0, CubemapFace.Unknown, i);
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