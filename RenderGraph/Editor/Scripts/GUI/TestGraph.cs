using HypnosRenderPipeline.RenderGraph;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

public class TestGraph
{
    [MenuItem("HypnosRenderPipeline/Try Execute HRG")]
    public static void Exe()
    {
        RenderGraphSaveFileWindow.OpenFileName openFileName = new RenderGraphSaveFileWindow.OpenFileName();
        openFileName.structSize = Marshal.SizeOf(openFileName);
        openFileName.templateName = "*.asset";
        openFileName.filter = "HRG(*.asset)\0*.asset";
        openFileName.file = new string(new char[256]);
        openFileName.maxFile = openFileName.file.Length;
        openFileName.fileTitle = new string(new char[64]);
        openFileName.maxFileTitle = openFileName.fileTitle.Length;
        openFileName.initialDir = Application.dataPath.Replace('/', '\\');
        openFileName.title = "Load HRG";
        openFileName.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008;

        if (RenderGraphSaveFileWindow.GetOpenFileName(openFileName))
        {
            string path = openFileName.file.Substring(openFileName.file.IndexOf("Assets"));
            path = path.Replace('\\', '/');
            if (!path.Contains(".asset"))
            {
                path += ".asset";
            }
            if (AssetDatabase.LoadAssetAtPath<RenderGraphInfo>(path) != null)
            {
                HRGDynamicExecutor executor = new HRGDynamicExecutor(AssetDatabase.LoadAssetAtPath<RenderGraphInfo>(path));
                executor.Excute(new HypnosRenderPipeline.RenderPass.RenderContext() { CmdBuffer = new UnityEngine.Rendering.CommandBuffer(), RenderCamera = Camera.main });
            }
        }
    }
}
