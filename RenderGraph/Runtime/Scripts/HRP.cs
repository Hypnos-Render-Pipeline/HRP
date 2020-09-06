using HypnosRenderPipeline.RenderGraph;
using HypnosRenderPipeline.RenderPass;
using System.Collections.Generic;
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
        
        Dictionary<Camera, int> clock;

#if UNITY_EDITOR

        MaterialWithName m_wireFrame = new MaterialWithName("Hidden/Wireframe");
        MaterialWithName m_iesSphere = new MaterialWithName("Hidden/IESSphere");

        HRGDynamicExecutor m_executor;
        RenderGraphInfo m_hypnosRenderPipelineGraph = null;
#endif

        public HypnosRenderPipeline(HypnosRenderPipelineAsset asset)
        {
            m_asset = asset;
            m_resourcePool = new RenderGraphResourcePool();
#if UNITY_EDITOR
            m_executor = new HRGDynamicExecutor(m_asset.hypnosRenderPipelineGraph);
#endif
            m_asset.defaultMaterial.hideFlags = HideFlags.NotEditable;

            clock = new Dictionary<Camera, int>();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
#if UNITY_EDITOR
            if (disposing)
            {
                m_executor.Dispose();
            }
#endif
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

                cb.SetGlobalMatrix("_V", cam.worldToCameraMatrix);
                cb.SetGlobalMatrix("_V_Inv", cam.cameraToWorldMatrix);
                cb.SetGlobalMatrix("_VP_Inv", (GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * cam.worldToCameraMatrix).inverse);
                if (clock.ContainsKey(cam)) {
                    cb.SetGlobalInt("_Clock", clock[cam]++);
                }
                else {
                    cb.SetGlobalInt("_Clock", 0);
                    clock[cam] = 0;
                }

                context.ExecuteCommandBuffer(cb);
                cb.Clear();

#if UNITY_EDITOR

                int result = -1;
                if (m_hypnosRenderPipelineGraph != m_asset.hypnosRenderPipelineGraph)
                {
                    m_hypnosRenderPipelineGraph = m_asset.hypnosRenderPipelineGraph;
                    m_executor.Dispose();
                    m_executor = new HRGDynamicExecutor(m_hypnosRenderPipelineGraph);
                }
                if (m_hypnosRenderPipelineGraph != null)
                {
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

                    result = m_executor.Excute(rc, debugCamera);
                }
                if (result == -1)
                {
                    result = Shader.PropertyToID("_TempResult");
                    cb.GetTemporaryRT(result, cam.pixelWidth, cam.pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                    cb.SetRenderTarget(result);
                    cb.ClearRenderTarget(true, true, Color.clear);
                }

                {
                    var vrender_cb = cam.GetCommandBuffers(CameraEvent.BeforeImageEffects);
                    if (vrender_cb.Length != 0) {
                        context.ExecuteCommandBuffer(vrender_cb[0]);
                        cb.Blit(BuiltinRenderTextureType.CameraTarget, result);
                    }
                }
#endif


#if UNITY_EDITOR
                if (cam.cameraType == CameraType.SceneView)
                {
                    ScriptableRenderContext.EmitWorldGeometryForSceneView(cam);
                    // this culling is to trigger sceneview gizmos culling
                    ScriptableCullingParameters cullingParams;
                    cam.TryGetCullingParameters(out cullingParams);
                    cullingParams.cullingMask = 0;
                    var cullingResults = context.Cull(ref cullingParams);

                    cb.SetRenderTarget(result);
                    var selected_lights = UnityEditor.Selection.GetFiltered<HRPLight>(UnityEditor.SelectionMode.Unfiltered);
                    var llist = new LightList();
                    LightManager.GetVisibleLights(llist, cam);
                    foreach (var light in llist.areas)
                    {
                        if (selected_lights.Contains(light))
                        {
                            if (light.lightType == HRPLightType.Mesh && light.lightMesh != null)
                            {
                                cb.DrawMesh(light.lightMesh, light.transform.localToWorldMatrix, m_wireFrame);
                            }
                        }
                    }
                    foreach (var light in llist.locals)
                    {
                        if (selected_lights.Contains(light))
                        {
                            if (light.supportIES && light.IESProfile != null)
                            {
                                cb.SetGlobalTexture("_IESCube", light.IESProfile);
                                cb.DrawMesh(MeshWithType.sphere, Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one * 0.6f), m_iesSphere);
                            }
                        }
                    }

                    context.ExecuteCommandBuffer(cb);
                    cb.Clear();
                }

                if (Handles.ShouldRenderGizmos() && (cam.cameraType == CameraType.SceneView || cam.cameraType == CameraType.Game))
                {
                    context.DrawGizmos(cam, GizmoSubset.PreImageEffects);
                    context.DrawGizmos(cam, GizmoSubset.PostImageEffects);
                    context.DrawUIOverlay(cam);
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
                        if (cam.targetTexture != null)
                            cb.Blit(result, cam.targetTexture);
                        else
                            cb.Blit(result, BuiltinRenderTextureType.CameraTarget);
                    }
                }
                else
                {
                    if (cam.targetTexture != null)
                        cb.Blit(result, cam.targetTexture);
                    else
                        cb.Blit(result, BuiltinRenderTextureType.CameraTarget);
                }
#else
                if (cam.targetTexture != null)
                    cb.Blit(result, cam.targetTexture);
                else
                    cb.Blit(result, BuiltinRenderTextureType.CameraTarget);
#endif

                cb.ReleaseTemporaryRT(result);
                context.ExecuteCommandBuffer(cb);
                cb.Clear();

                EndCameraRendering(context, cam);
            }

            EndFrameRendering(context, cameras);

            context.Submit();
        }
    }
}