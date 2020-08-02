using HypnosRenderPipeline.RenderGraph;
using HypnosRenderPipeline.RenderPass;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline
{
    public class HypnosRenderPipeline : RenderPipeline
    {
        HypnosRenderPipelineAsset m_asset;
        RenderGraphResourcePool m_resourcePool;

        Material __m_wireFrame__;
        Material m_wireFrame { get { if (__m_wireFrame__ == null) __m_wireFrame__ = new Material(Shader.Find("Hidden/Wireframe")); return __m_wireFrame__; } }

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

                context.SetupCameraProperties(cam);

                rc.RenderCamera = cam;
                rc.CmdBuffer = cb;

#if UNITY_EDITOR
                m_executor = new HRGDynamicExecutor(m_asset.hypnosRenderPipelineGraph);
                int result = m_executor.Excute(rc);
                if (result == -1) return;
#endif


#if UNITY_EDITOR
                if (cam.cameraType == CameraType.SceneView)
                {
                    ScriptableRenderContext.EmitWorldGeometryForSceneView(cam);
                    // this culling is to trigger sceneview gizmos culling
                    ScriptableCullingParameters cullingParams;
                    cam.TryGetCullingParameters(out cullingParams);
                    var cullingResults = context.Cull(ref cullingParams);

                    cb.SetRenderTarget(result);
                    var selected_lights = Selection.GetFiltered<Light>(SelectionMode.Unfiltered);
                    foreach (var light in cullingResults.visibleLights)
                    {
                        var ld = light.light.GetComponent<HRPLight>();
                        if (ld != null)
                        {
                            if (selected_lights.Contains(light.light) && ld.lightType == HRPLightType.Mesh && ld.lightMesh != null)
                            {
                                cb.DrawMesh(ld.lightMesh, ld.transform.localToWorldMatrix, m_wireFrame);
                            }
                        }
                    }

                    context.ExecuteCommandBuffer(cb);
                    cb.Clear();
                    context.DrawGizmos(cam, GizmoSubset.PreImageEffects);
                    context.DrawGizmos(cam, GizmoSubset.PostImageEffects);
                    context.DrawUIOverlay(cam);
                }
#endif

                if (cam.targetTexture != null)
                    cb.Blit(result, cam.targetTexture);
                else
                    cb.Blit(result, BuiltinRenderTextureType.CameraTarget);

                context.ExecuteCommandBuffer(cb);
                cb.Clear();


                EndCameraRendering(context, cam);
            }

            EndFrameRendering(context, cameras);

            context.Submit();
        }
    }
}