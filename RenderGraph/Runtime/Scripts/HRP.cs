using HypnosRenderPipeline.RenderGraph;
using HypnosRenderPipeline.RenderPass;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline
{
    public class HypnosRenderPipeline : RenderPipeline
    {
        HypnosRenderPipelineAsset m_asset;
        RenderGraphResourcePool m_resourcePool;

#if UNITY_EDITOR
        HRGDynamicExecutor m_executor;
#endif

        public HypnosRenderPipeline(HypnosRenderPipelineAsset asset)
        {
            m_asset = asset;
            m_resourcePool = new RenderGraphResourcePool();
        }


        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            var rc = new RenderContext() { Context = context, ResourcePool = m_resourcePool };

            CommandBuffer cb = new CommandBuffer();

            BeginFrameRendering(context, cameras);

            foreach (var cam in cameras)
            {
                BeginCameraRendering(context, cam);

#if UNITY_EDITOR
                if (cam.cameraType == CameraType.SceneView)
                {
                    ScriptableRenderContext.EmitWorldGeometryForSceneView(cam);
                    // this culling is to trigger sceneview gizmos culling
                    ScriptableCullingParameters cullingParams;
                    cam.TryGetCullingParameters(out cullingParams);
                    cullingParams.cullingMask = 0;
                    var cullingResults = context.Cull(ref cullingParams);
                }
#endif

                context.SetupCameraProperties(cam);

                cb.Clear();
                rc.RenderCamera = cam;
                rc.CmdBuffer = cb;

#if UNITY_EDITOR
                m_executor = new HRGDynamicExecutor(m_asset.hypnosRenderPipelineGraph);
                m_executor.Excute(rc);
#endif

                if (cam.targetTexture != null)
                    cb.Blit(BuiltinRenderTextureType.CameraTarget, cam.targetTexture);

                context.ExecuteCommandBuffer(cb);

#if UNITY_EDITOR
                if (cam.cameraType == CameraType.SceneView)
                {
                    cb.Clear();
                    cb.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                    context.ExecuteCommandBuffer(cb);
                    context.DrawGizmos(cam, GizmoSubset.PreImageEffects);
                    context.DrawGizmos(cam, GizmoSubset.PostImageEffects);
                    context.DrawUIOverlay(cam);
                }
#endif

                EndCameraRendering(context, cam);
            }

            EndFrameRendering(context, cameras);

            context.Submit();
        }
    }
}