using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class NormalImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        var impor = assetImporter as TextureImporter;
        if (impor.textureType == TextureImporterType.NormalMap)
        {
            impor.compressionQuality = 100;
            impor.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }

    void OnPostprocessTexture(Texture2D tex)
    {
        TextureImporter impor = this.assetImporter as TextureImporter;
        if (impor.textureType == TextureImporterType.NormalMap)
        {
            if (tex.width != tex.height || !Mathf.IsPowerOfTwo(tex.width))
                Debug.LogError("Only support width = height and pow of two normal map.");
            Vector2Int wh = new Vector2Int(tex.width, tex.height);
            Debug.Log(wh);
            Vector3[,] normals = new Vector3[wh.x, wh.y];
            {
                Color[] colors = tex.GetPixels();
                for (int i = 0; i < wh.x; i++)
                {
                    for (int j = 0; j < wh.y; j++)
                    {
                        int index = j * wh.x + i;
                        var color = colors[index];
                        Vector3 normal = new Vector3(color.r, color.g, color.b);
                        normal.x = color.a * 2 - 1;
                        normal.y = normal.y * 2 - 1;
                        normal.z = Mathf.Sqrt(1 - Mathf.Clamp01(normal.x * normal.x + normal.y * normal.y));
                        normals[i, j] = normal.normalized;
                        color.r = 0;
                        colors[index] = color;
                    }
                }
                tex.SetPixels(colors, 0);
            }

            for (int mip = 1; mip < tex.mipmapCount; mip++)
            {
                wh /= 2;
                int size = 1 << mip;
                Color[] colors = tex.GetPixels(mip);

                for (int pix = 0; pix < colors.Length; pix++)
                {
                    Vector2Int xy = new Vector2Int(pix % wh.x, pix / wh.x);
                    Vector3 normal = Vector3.zero;
                    for (int i = 0; i < size; i++)
                    {
                        for (int j = 0; j < size; j++)
                        {
                            var index = xy * size + new Vector2Int(i, j);
                            normal += normals[index.x, index.y];
                        }
                    }

                    float k = (1 - normal.magnitude / size / size);
                    k *= Mathf.Pow((float)mip / tex.mipmapCount, 0.5f);
                    colors[pix].r = k;
                }
                tex.SetPixels(colors, mip);
            }
        }
    }
}
