using HypnosRenderPipeline.RenderGraph;
using HypnosRenderPipeline.RenderPass;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

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
#if UNITY_EDITOR
            m_executor = new HRGDynamicExecutor(m_asset.hypnosRenderPipelineGraph);
#endif
            m_asset.defaultMaterial.hideFlags = HideFlags.NotEditable;
        }


        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            var rc = new RenderContext() { Context = context, ResourcePool = m_resourcePool };

            CommandBuffer cb = new CommandBuffer();

            BeginFrameRendering(context, cameras);

#if UNITY_EDITOR
            // determinate debug priority
            var hasSceneCamera = false;
            if (UnityEditor.SceneView.sceneViews.Count != 0)
            {
                foreach (var sv in UnityEditor.SceneView.sceneViews)
                {
                    if ((sv as UnityEditor.SceneView).hasFocus) hasSceneCamera = true;
                }
            }

            UnityEditor.EditorWindow[] windows = Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>();
            var gameWindow = windows.FirstOrDefault(e => e.titleContent.text.Contains("Game"));
            var hasGameCamera = (gameWindow != null) && gameWindow.hasFocus;
#endif

            foreach (var cam in cameras)
            {
                BeginCameraRendering(context, cam);

                context.SetupCameraProperties(cam);

                rc.RenderCamera = cam;
                rc.CmdBuffer = cb;

#if UNITY_EDITOR
                // determinate whether debug this camera
                bool debugCamera = false;
                if (hasGameCamera)
                {
                    if (cam.cameraType == CameraType.Game) debugCamera = true;
                }
                
                else if (hasSceneCamera)
                {
                    if (cam.cameraType == CameraType.SceneView) debugCamera = true;
                }
                else if (cam == cameras[0]) debugCamera = true;

                int result = m_executor.Excute(rc, debugCamera);
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
                    var selected_lights = UnityEditor.Selection.GetFiltered<Light>(UnityEditor.SelectionMode.Unfiltered);
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
                }
#endif

#if UNITY_EDITOR
                if (cam.cameraType == CameraType.SceneView)
                {
                    var drawMode = SceneView.lastActiveSceneView.cameraMode.drawMode;
                    if (drawMode == DrawCameraMode.Wireframe)
                    {
                        cb.SetGlobalFloat("_Multiplier", 1);
                        cb.SetGlobalInt("_Channel", 4);
                        if (cam.targetTexture != null)
                            cb.Blit(result, cam.targetTexture, MaterialWithName.debugBlit);
                        else
                            cb.Blit(result, BuiltinRenderTextureType.CameraTarget, MaterialWithName.debugBlit);
                    }
                    else
                    {
#endif
                        if (cam.targetTexture != null)
                            cb.Blit(result, cam.targetTexture);
                        else
                            cb.Blit(result, BuiltinRenderTextureType.CameraTarget);

#if UNITY_EDITOR
                    }

                    context.ExecuteCommandBuffer(cb);
                    cb.Clear();
                    context.DrawGizmos(cam, GizmoSubset.PreImageEffects);
                    context.DrawGizmos(cam, GizmoSubset.PostImageEffects);
                    context.DrawUIOverlay(cam);
                }
                else
                {
                    if (cam.targetTexture != null)
                        cb.Blit(result, cam.targetTexture);
                    else
                        cb.Blit(result, BuiltinRenderTextureType.CameraTarget);
                }
#endif

                context.ExecuteCommandBuffer(cb);
                cb.Clear();

                EndCameraRendering(context, cam);
            }

            EndFrameRendering(context, cameras);

            context.Submit();
        }
    }
}