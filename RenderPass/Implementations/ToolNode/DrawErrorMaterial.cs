using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{

    public class DrawErrorMaterials : BaseToolNode
    {

        [NodePin]
        public TexturePin target = new TexturePin(new TexturePinDesc(
                                                    new RenderTextureDescriptor(1, 1, RenderTextureFormat.DefaultHDR, 0),
                                                    TexturePinDesc.SizeCastMode.ResizeToInput,
                                                    TexturePinDesc.ColorCastMode.FitToInput,
                                                    TexturePinDesc.SizeScale.Full));

        [NodePin]
        public TexturePin depth = new TexturePin(new TexturePinDesc(
                                            new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                            TexturePinDesc.SizeCastMode.ResizeToInput,
                                            TexturePinDesc.ColorCastMode.Fixed,
                                            TexturePinDesc.SizeScale.Full));

        static ShaderTagId[] legacyShaderTagIds = {
                    new ShaderTagId("Always"),
                    new ShaderTagId("ForwardBase"),
                    new ShaderTagId("PrepassBase"),
                    new ShaderTagId("Vertex"),
                    new ShaderTagId("VertexLMRGBM"),
                    new ShaderTagId("VertexLM")
                };

        static Material __errorMat__;
        static Material errorMat { get { if (__errorMat__ == null) __errorMat__ = new Material(Shader.Find("Hidden/InternalErrorShader")); return __errorMat__; } }

        public override void Excute(RenderContext context)
        {
            context.CmdBuffer.SetRenderTarget(color: target.handle, depth: depth.handle);
            context.Context.ExecuteCommandBuffer(context.CmdBuffer);
            context.CmdBuffer.Clear();

            ScriptableCullingParameters cullingParams;
            context.RenderCamera.TryGetCullingParameters(out cullingParams);
            var cullingResults = context.Context.Cull(ref cullingParams);

            foreach (var name in legacyShaderTagIds)
            {
                var a = new DrawingSettings(name, new SortingSettings(context.RenderCamera)) { overrideMaterial = errorMat };
                var b = FilteringSettings.defaultValue;

                context.Context.DrawRenderers(cullingResults, ref a, ref b);
            }
        }
    }

}