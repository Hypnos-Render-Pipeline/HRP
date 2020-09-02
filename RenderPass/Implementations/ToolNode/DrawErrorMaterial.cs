using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{

    public class DrawErrorMaterials : BaseToolNode
    {

        [NodePin]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.DefaultHDR, 0),
                                                    SizeCastMode.ResizeToInput,
                                                    ColorCastMode.FitToInput,
                                                    SizeScale.Full);

        [NodePin]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                                    SizeCastMode.ResizeToInput,
                                                    ColorCastMode.Fixed,
                                                    SizeScale.Full);

        static ShaderTagId[] legacyShaderTagIds = {
                    new ShaderTagId("Always"),
                    new ShaderTagId("ForwardBase"),
                    new ShaderTagId("PrepassBase"),
                    new ShaderTagId("Vertex"),
                    new ShaderTagId("VertexLMRGBM"),
                    new ShaderTagId("VertexLM")
                };

        static MaterialWithName errorMat = new MaterialWithName("Hidden/InternalErrorShader");

        public override void Excute(RenderContext context)
        {
            context.CmdBuffer.SetRenderTarget(color: target, depth: depth);
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