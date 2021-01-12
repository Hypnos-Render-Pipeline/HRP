using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class NormalImporter : AssetPostprocessor
{
    void OnPostprocessTexture(Texture2D tex)
    {
        TextureImporter impor = this.assetImporter as TextureImporter;
        //if (tex.name != "baseColor_0") return;
        Color[] colors = tex.GetPixels();

        for (int i = 1; i < tex.mipmapCount; i++)
        {
            tex.SetPixels(Resources.Load<Texture2D>("Tex/normal_" + i.ToString()).GetPixels(), i);
        }
        Debug.Log("A");
    }
}
