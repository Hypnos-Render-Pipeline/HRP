using System.Collections;
using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using System.IO;
using UnityEditor;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections;


public class OutputImage
{
    public OutputImage()
    {
        string path = Application.streamingAssetsPath + "/VRenderOutput/" + SceneManager.GetActiveScene().name + "/";

        if (!File.Exists(path))
            Directory.CreateDirectory(path);
    }

    public IEnumerator SaveAOVs(Camera cam, string name, RenderTexture a, RenderTexture n, RenderTexture l)
    {
        string path = Application.streamingAssetsPath + "/VRenderOutput/" + SceneManager.GetActiveScene().name + "/";

        if (!File.Exists(path))
            Directory.CreateDirectory(path);

        name += "_" + Camera.main.name + "_" + System.DateTime.Now.ToString().Replace(" ", "_").Replace("\\", "_").Replace("/", "_").Replace(":", "-");
        path += name;

        Debug.Log("Save0");
        yield return new WaitForEndOfFrame();
        Debug.Log("Save1");

        Texture2D res = new Texture2D(cam.pixelWidth, cam.pixelHeight, UnityEngine.Experimental.Rendering.DefaultFormat.HDR, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
        Texture2D linear = new Texture2D(l.width, l.height, UnityEngine.Experimental.Rendering.DefaultFormat.HDR, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
        Texture2D ldr = new Texture2D(a.width, a.height, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
        Texture2D depthNormal = new Texture2D(n.width, n.height, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);

        {
            res.ReadPixels(new Rect(0, 0, res.width, res.height), 0, 0, false);
            res.Apply();

            byte[] bytes = res.EncodeToEXR();
            File.WriteAllBytes(path + "_FinalResult.exr", bytes);
        }

        {
            RenderTexture.active = l;
            linear.ReadPixels(new Rect(0, 0, linear.width, linear.height), 0, 0, false);
            linear.Apply();

            name += "_" + Camera.main.name + "_" + System.DateTime.Now.ToString().Replace(" ", "_").Replace("\\", "_").Replace("/", "_").Replace(":", "-");

            byte[] bytes = linear.EncodeToEXR();
            File.WriteAllBytes(path + "_LinearResult.exr", bytes);
        }

        {
            RenderTexture.active = a;
            ldr.ReadPixels(new Rect(0, 0, ldr.width, ldr.height), 0, 0, false);
            ldr.Apply();
            byte[] bytes = ldr.EncodeToPNG();
            File.WriteAllBytes(path + "_Albedo.png", bytes);
        }

        {
            RenderTexture.active = n;
            depthNormal.ReadPixels(new Rect(0, 0, depthNormal.width, depthNormal.height), 0, 0, false);
            depthNormal.Apply();
            byte[] bytes = depthNormal.EncodeToEXR();
            File.WriteAllBytes(path + "_NormalDepth.exr", bytes);
        }

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
        yield return null;
    }
}