using HypnosRenderPipeline.RenderGraph;
using HypnosRenderPipeline.RenderPass;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline
{
    public class HypnosRenderPipeline : RenderPipeline
    {
        HypnosRenderPipelineAsset m_asset;

        class PerCameraData
        {
            public Matrix4x4 lastVP;
            public int clock;
        }

        Dictionary<Camera, PerCameraData> clock;
        Dictionary<Camera, RenderGraphResourcesPool> resourcesPool;

        ScriptableCullingParameters defaultCullingParams;

#if UNITY_EDITOR

        MaterialWithName m_wireFrame = new MaterialWithName("Hidden/Wireframe");
        MaterialWithName m_iesSphere = new MaterialWithName("Hidden/IESSphere");

        HRGDynamicExecutor m_executor;
        RenderGraphInfo m_hypnosRenderPipelineGraph = null;
#endif

        public HypnosRenderPipeline(HypnosRenderPipelineAsset asset)
        {
            m_asset = asset;
            //m_resourcePool = new RenderGraphResourcePool();
#if UNITY_EDITOR
            m_executor = new HRGDynamicExecutor(m_asset.hypnosRenderPipelineGraph);
#endif
            m_asset.defaultMaterial.hideFlags = HideFlags.NotEditable;

            clock = new Dictionary<Camera, PerCameraData>();
            resourcesPool = new Dictionary<Camera, RenderGraphResourcesPool>();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
#if UNITY_EDITOR
            if (disposing)
            {
                m_executor.Dispose();
                foreach (var pair in resourcesPool)
                {
                    pair.Value.Dispose();
                }
                resourcesPool.Clear();
            }
#endif
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            CommandBufferExtension.SetupContext(context);

            var rc = new RenderContext() { context = context };

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

                rc.camera = cam;
                rc.commandBuffer = cb;
                cam.TryGetCullingParameters(out defaultCullingParams);
                rc.defaultCullingResult = context.Cull(ref defaultCullingParams);

                Matrix4x4 v = cam.worldToCameraMatrix, p = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
                cb.SetGlobalMatrix("_V", v);
                cb.SetGlobalMatrix("_P", p);
                cb.SetGlobalMatrix("_V_Inv", cam.cameraToWorldMatrix);
                cb.SetGlobalMatrix("_P_Inv", p.inverse);
                var vp = p * v;
                cb.SetGlobalMatrix("_VP", vp);
                cb.SetGlobalMatrix("_VP_Inv", vp.inverse);
                if (clock.ContainsKey(cam)) {
                    cb.SetGlobalInt("_Clock", clock[cam].clock++);
                }
                else {
                    cb.SetGlobalInt("_Clock", 0);
                    var pcd = new PerCameraData();
                    pcd.clock = 0;
                    pcd.lastVP = vp;
                    clock[cam] = pcd;
                }
                cb.SetGlobalMatrix("_Last_VP", clock[cam].lastVP);
                clock[cam].lastVP = vp;
                RenderGraphResourcesPool pool;
                if (!resourcesPool.ContainsKey(cam))
                {
                    pool = new RenderGraphResourcesPool();
                    resourcesPool.Add(cam, pool);
                }
                else
                {
                    pool = resourcesPool[cam];
                }
                rc.frameIndex = clock[cam].clock;
                rc.resourcesPool = pool;

                context.ExecuteCommandBuffer(cb);
                cb.Clear();

#if UNITY_EDITOR

                int result = -1;                
                {
                    var vrender_cb = cam.GetCommandBuffers(CameraEvent.BeforeImageEffects);
                    if (vrender_cb.Length != 0) {

                        result = Shader.PropertyToID("_TempResult");
                        cb.GetTemporaryRT(result, cam.pixelWidth, cam.pixelHeight, 24, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                        cb.SetRenderTarget(result);
                        cb.ClearRenderTarget(true, true, Color.clear);
                        cb.SetRenderTarget(result);

                        var a = new DrawingSettings(new ShaderTagId("PreZ"), new SortingSettings(cam));
                        var b = FilteringSettings.defaultValue;
                        b.renderQueueRange = RenderQueueRange.all;

                        cb.DrawRenderers(rc.defaultCullingResult, ref a, ref b);

                        context.ExecuteCommandBuffer(vrender_cb[0]);
                        cb.Blit(BuiltinRenderTextureType.CameraTarget, result);
                    }
                    else
                    {
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