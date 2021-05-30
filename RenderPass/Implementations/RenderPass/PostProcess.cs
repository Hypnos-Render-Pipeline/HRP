using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace HypnosRenderPipeline.RenderPass
{    
    [RenderGraph.RenderNodeInformation("Image Post Process.")]
    public class PostProcess : BaseRenderPass
    {
        [NodePin(PinType.InOut, true)]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGBHalf));

        [NodePin(PinType.In)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        [NodePin(PinType.In)]
        public TexturePin motion = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.RGFloat, 0), colorCastMode: ColorCastMode.Fixed);

        public override void Execute(RenderContext context)
        {

            PostProcessLayer m_CameraPostProcessLayer = context.camera.GetComponent<PostProcessLayer>();
            bool hasPostProcessing = m_CameraPostProcessLayer != null;
            bool usePostProcessing = false;
            PostProcessRenderContext m_PostProcessRenderContext = null;
            if (hasPostProcessing)
            {
                m_PostProcessRenderContext = new PostProcessRenderContext() {  };
                usePostProcessing = m_CameraPostProcessLayer.enabled;
            }
            if (usePostProcessing)
            {
                int tempTarget = Shader.PropertyToID("_TempTarget");
                context.commandBuffer.GetTemporaryRT(tempTarget, target.desc.basicDesc);
                context.commandBuffer.SetGlobalTexture("_CameraColorTexture", target);
                context.commandBuffer.SetGlobalTexture("_CameraDepthTexture", depth);
                context.commandBuffer.SetGlobalTexture("_CameraMotionVectorsTexture", motion);
                m_PostProcessRenderContext.Reset();
                m_PostProcessRenderContext.camera = context.camera;
                m_PostProcessRenderContext.source = target;
                m_PostProcessRenderContext.sourceFormat = target.desc.basicDesc.colorFormat;
                m_PostProcessRenderContext.destination = tempTarget;
                m_PostProcessRenderContext.command = context.commandBuffer;
                m_PostProcessRenderContext.flip = false;
                m_CameraPostProcessLayer.Render(m_PostProcessRenderContext);

                context.commandBuffer.CopyTexture(tempTarget, target);
                context.commandBuffer.ReleaseTemporaryRT(tempTarget);
            }
        }
    }
}