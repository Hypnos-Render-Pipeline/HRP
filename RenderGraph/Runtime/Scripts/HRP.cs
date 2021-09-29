using HypnosRenderPipeline.RenderGraph;
using HypnosRenderPipeline.RenderPass;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HypnosRenderPipeline.Tools;
using UnityEngine.Rendering.PostProcessing;

namespace HypnosRenderPipeline
{
    public class HypnosRenderPipeline : RenderPipeline
    {
        HypnosRenderPipelineAsset m_asset;

        class PerCameraData
        {
            public Matrix4x4 lastVP;
            public Matrix4x4 lastVP_nojittered;
            public Vector2 lastJitter;
            public int clock;
        }

        Dictionary<int, PerCameraData> clock;
        Dictionary<int, RenderGraphResourcesPool> resourcesPool;

        ScriptableCullingParameters defaultCullingParams;

#if UNITY_EDITOR

        MaterialWithName m_wireFrame = new MaterialWithName("Hidden/Wireframe");
        MaterialWithName m_iesSphere = new MaterialWithName("Hidden/IESSphere");
#endif

        HRGExecutor m_executor;
        HypnosRenderGraph m_hypnosRenderPipelineGraph = null;

        public HypnosRenderPipeline(HypnosRenderPipelineAsset asset)
        {
            m_asset = asset;
            Shader.globalRenderPipeline = "HypnosRenderPipeline";
            GraphicsSettings.useScriptableRenderPipelineBatching = true;

#if UNITY_EDITOR
            if (m_asset.useCompliedCodeInEditor)
            {
                try
                {
#endif
                    m_executor = HRGCompiler.CompileFromString(m_asset.hypnosRenderPipelineGraph.name, m_asset.hypnosRenderPipelineGraph.code);
#if UNITY_EDITOR
                }
                catch
                {
                    // try totally recompile
                    HRGCompiler.Compile(m_asset.hypnosRenderPipelineGraph);
                    m_executor = HRGCompiler.CompileFromString(m_asset.hypnosRenderPipelineGraph.name, m_asset.hypnosRenderPipelineGraph.code);
                }
            }
            else
            {
                m_executor = new HRGDynamicExecutor(m_asset.hypnosRenderPipelineGraph);
            }
#endif
            m_executor.Init();
            m_asset.defaultMaterial.hideFlags = HideFlags.NotEditable;

            clock = new Dictionary<int, PerCameraData>();
            resourcesPool = new Dictionary<int, RenderGraphResourcesPool>();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                m_executor.Dispose();
                foreach (var pair in resourcesPool)
                {
                    pair.Value.Dispose();
                }
                resourcesPool.Clear();
            }
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            CommandBufferExtension.SetupContext(context);

            var rc = new RenderContext() { context = context };

            var acc = RTRegister.AccStruct();
#if UNITY_2020_2_OR_NEWER
            acc.Build();
#else
            acc.Update();
#endif
            rc.defaultAcc = acc;

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
                int cameraId = cam.GetHashCode();
#if UNITY_EDITOR
                if (cam.cameraType == CameraType.Preview)
                {
                    if (cam.pixelHeight == 64) cameraId += 1;
                    // Unity will use one PreviewCamera to draw Material icon and Material Preview together, this will cause resources identity be confused.
                    // We found that the Material preview can not be less than 70 pixel, and the icon is always 64, so we use this to distinguish them.
                }
                int renderNum = 1;
                if (cam.cameraType == CameraType.Preview)
                {
                    renderNum = 2;
                }



                for (int renderIndex = 0; renderIndex < renderNum; renderIndex++)
                {
#endif
                    BeginCameraRendering(context, cam);

                    RenderTexture original_target_tex = null;
                    RenderTexture place_holder_tex = null;
                    if (m_asset.enableDLSS
#if UNITY_EDITOR
                    && cam.cameraType == CameraType.Game
#endif
                    )
                    {
                        place_holder_tex = RenderTexture.GetTemporary(new RenderTextureDescriptor(cam.pixelWidth / 2, cam.pixelHeight / 2, RenderTextureFormat.DefaultHDR));
                        original_target_tex = cam.targetTexture;
                        cam.targetTexture = place_holder_tex;
                        rc.enableDLSS = true;
                    }
                    else
                        rc.enableDLSS = false;

                    if (cam.cameraType == CameraType.Game)
                    {
                        PostProcessLayer cameraPostProcessLayer = cam.GetComponent<PostProcessLayer>();
                        bool hasPostProcessing = cameraPostProcessLayer != null;
                        bool usePostProcessing = false;
                        PostProcessRenderContext m_PostProcessRenderContext = null;
                        if (hasPostProcessing)
                        {
                            m_PostProcessRenderContext = new PostProcessRenderContext() { };
                            usePostProcessing = cameraPostProcessLayer.enabled;
                        }
                        if (usePostProcessing && cameraPostProcessLayer.antialiasingMode == PostProcessLayer.Antialiasing.TemporalAntialiasing)
                        {
                            cameraPostProcessLayer.temporalAntialiasing.ConfigureJitteredProjectionMatrix(new PostProcessRenderContext() { camera = cam });
                            rc.jitter = cameraPostProcessLayer.temporalAntialiasing.jitter;
                        }
                        else
                        {
                            rc.jitter = Vector2.zero;
                        }
                    }

                    context.SetupCameraProperties(cam);

                    rc.camera = cam;
                    rc.commandBuffer = cb;
                    cam.TryGetCullingParameters(out defaultCullingParams);
                    rc.defaultCullingResult = cb.Cull(ref defaultCullingParams);

                    Matrix4x4 v = cam.worldToCameraMatrix, p = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false), np = GL.GetGPUProjectionMatrix(cam.nonJitteredProjectionMatrix, false);
                    cb.SetGlobalMatrix("_V", v);
                    cb.SetGlobalMatrix("_P", p);
                    cb.SetGlobalMatrix("_P_NoJitter", np);
                    cb.SetGlobalMatrix("_V_Inv", cam.cameraToWorldMatrix);
                    cb.SetGlobalMatrix("_P_Inv", p.inverse);
                    var vp = p * v;
                    var vpn = np * v;
                    cb.SetGlobalMatrix("_VP", vp);
                    cb.SetGlobalMatrix("_VP_NoJitter", vpn);
                    cb.SetGlobalMatrix("_VP_", GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * v);
                    cb.SetGlobalMatrix("_VP_Inv", vp.inverse);
                    cb.SetGlobalMatrix("_VP_Inv_NoJitter", vpn.inverse);
                    if (clock.ContainsKey(cameraId))
                    {
                        cb.SetGlobalInt("_Clock", clock[cameraId].clock++);
                    }
                    else
                    {
                        cb.SetGlobalInt("_Clock", 0);
                        var pcd = new PerCameraData();
                        pcd.clock = 0;
                        pcd.lastVP = vp;
                        clock[cameraId] = pcd;
                    }
                    cb.SetGlobalMatrix("_Last_VP", clock[cameraId].lastVP);
                    cb.SetGlobalMatrix("_Last_VP_Inv", clock[cameraId].lastVP.inverse);
                    cb.SetGlobalMatrix("_Last_VP_NoJitter", clock[cameraId].lastVP_nojittered);
                    cb.SetGlobalMatrix("_Last_VP_Inv_NoJitter", clock[cameraId].lastVP_nojittered.inverse);
                    cb.SetGlobalVector("_JitterOffset", rc.jitter - clock[cameraId].lastJitter);
                    clock[cameraId].lastJitter = rc.jitter;
                    clock[cameraId].lastVP = vp;
                    clock[cameraId].lastVP_nojittered = vpn;
                    RenderGraphResourcesPool pool;
                    if (!resourcesPool.ContainsKey(cameraId))
                    {
                        pool = new RenderGraphResourcesPool();
                        resourcesPool.Add(cameraId, pool);
                    }
                    else
                    {
                        pool = resourcesPool[cameraId];
                    }
                    rc.frameIndex = clock[cameraId].clock;
                    rc.resourcesPool = pool;

                    context.ExecuteCommandBuffer(cb);
                    cb.Clear();

                    int result = -1;

                    {
                        var vrender_cb = cam.GetCommandBuffers(CameraEvent.BeforeImageEffects);
                        if (vrender_cb.Length != 0)
                        {

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

                            PostProcessLayer m_CameraPostProcessLayer = cam.GetComponent<PostProcessLayer>();
                            bool hasPostProcessing = m_CameraPostProcessLayer != null;
                            bool usePostProcessing = false;
                            PostProcessRenderContext m_PostProcessRenderContext = null;
                            if (hasPostProcessing)
                            {
                                m_PostProcessRenderContext = new PostProcessRenderContext() { };
                                usePostProcessing = m_CameraPostProcessLayer.enabled;
                            }
                            if (usePostProcessing)
                            {
                                m_PostProcessRenderContext.Reset();
                                m_PostProcessRenderContext.camera = cam;
                                m_PostProcessRenderContext.source = BuiltinRenderTextureType.CameraTarget;
                                m_PostProcessRenderContext.sourceFormat = RenderTextureFormat.DefaultHDR;
                                m_PostProcessRenderContext.destination = result;
                                m_PostProcessRenderContext.command = cb;
                                m_PostProcessRenderContext.flip = false;
                                m_CameraPostProcessLayer.Render(m_PostProcessRenderContext);
                            }
                            else
                            {
                                cb.Blit(BuiltinRenderTextureType.CameraTarget, result);
                            }
                        }
                        else
                        {
                            if (m_hypnosRenderPipelineGraph != m_asset.hypnosRenderPipelineGraph
#if UNITY_EDITOR
                            || m_hypnosRenderPipelineGraph.recompiled
#endif
                            )
                            {
                                m_hypnosRenderPipelineGraph = m_asset.hypnosRenderPipelineGraph;
#if UNITY_EDITOR
                                m_hypnosRenderPipelineGraph.recompiled = false;
#endif
                                m_executor.Dispose();
#if UNITY_EDITOR
                                if (m_asset.useCompliedCodeInEditor)
                                {
                                    try
                                    {
#endif
                                        m_executor = HRGCompiler.CompileFromString(m_asset.hypnosRenderPipelineGraph.name, m_asset.hypnosRenderPipelineGraph.code);
#if UNITY_EDITOR
                                    }
                                    catch
                                    {
                                        // try totally recompile
                                        HRGCompiler.Compile(m_asset.hypnosRenderPipelineGraph);
                                        m_executor = HRGCompiler.CompileFromString(m_asset.hypnosRenderPipelineGraph.name, m_asset.hypnosRenderPipelineGraph.code);
                                    }
                                }
                                else
                                {
                                    m_executor = new HRGDynamicExecutor(m_asset.hypnosRenderPipelineGraph);
                                }
#endif
                                m_executor.Init();
                            }
                            if (m_hypnosRenderPipelineGraph != null)
                            {
                                // determinate whether debug this camera
                                bool debugCamera = false;
#if UNITY_EDITOR
                                if (hasGameCamera)
                                {
                                    if (cam.cameraType == CameraType.Game) debugCamera = true;
                                }

                                else if (hasSceneCamera)
                                {
                                    if (cam.cameraType == CameraType.SceneView) debugCamera = true;
                                }
                                else if (cam == cameras[0]) debugCamera = true;
#endif
                                result = m_executor.Execute(rc, debugCamera);
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

                    if (m_asset.enableDLSS
#if UNITY_EDITOR
                    && cam.cameraType == CameraType.Game
#endif
                    )
                    {
                        cb.Blit(place_holder_tex, BuiltinRenderTextureType.CameraTarget);
                        cam.targetTexture = original_target_tex;
                        RenderTexture.ReleaseTemporary(place_holder_tex);
                    }

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

                    cam.ResetProjectionMatrix();
                    EndCameraRendering(context, cam);
#if UNITY_EDITOR
                }
#endif
            }

            EndFrameRendering(context, cameras);

            context.Submit();
        }
    }
}