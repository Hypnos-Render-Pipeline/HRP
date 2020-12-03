using HypnosRenderPipeline.RenderPass;
using UnityEngine;

namespace HypnosRenderPipeline.RenderGraph
{
    public class HRG_SSR : HRGExecutor
    {
        // Nodes:
        // ----------------------------
        GBuffer GBuffer290612621;
        OutputNode OutputNode602049860;
        CullLight CullLight446935086;
        ZBin ZBin949438803;
        RTDI RTDI127590340;
        CombineColorDepth CombineColorDepth760392706;
        AreaLight AreaLight145779702;
        SunAtmo SunAtmo970969601;
        SkyBoxIBL SkyBoxIBL669583313;
        HiDepth HiDepth573432727;
        RTSpecular RTSpecular673834354;
        PyramidColor PyramidColor490550593;
        Transparent Transparent230075522;
        RTGI RTGI70063463;
        PyramidColor PyramidColor625127020;
        // ----------------------------

        // ShaderIDs:
        // ----------------------------
        System.Int32 GBuffer290612621_depth = Shader.PropertyToID("GBuffer290612621.depth");
        System.Int32 GBuffer290612621_diffuse = Shader.PropertyToID("GBuffer290612621.diffuse");
        System.Int32 GBuffer290612621_specular = Shader.PropertyToID("GBuffer290612621.specular");
        System.Int32 GBuffer290612621_normal = Shader.PropertyToID("GBuffer290612621.normal");
        System.Int32 GBuffer290612621_emission = Shader.PropertyToID("GBuffer290612621.emission");
        System.Int32 GBuffer290612621_microAO = Shader.PropertyToID("GBuffer290612621.microAO");
        System.Int32 GBuffer290612621_motion = Shader.PropertyToID("GBuffer290612621.motion");
        System.Int32 OutputNode602049860_result = Shader.PropertyToID("OutputNode602049860.result");
        System.Int32 CullLight446935086_lights = Shader.PropertyToID("CullLight446935086.lights");
        System.Int32 ZBin949438803_lights = Shader.PropertyToID("ZBin949438803.lights");
        System.Int32 ZBin949438803_depth = Shader.PropertyToID("ZBin949438803.depth");
        System.Int32 ZBin949438803_lightBuffer = Shader.PropertyToID("ZBin949438803.lightBuffer");
        System.Int32 ZBin949438803_tileLights = Shader.PropertyToID("ZBin949438803.tileLights");
        System.Int32 RTDI127590340_lights = Shader.PropertyToID("RTDI127590340.lights");
        System.Int32 RTDI127590340_lightBuffer = Shader.PropertyToID("RTDI127590340.lightBuffer");
        System.Int32 RTDI127590340_tiledLights = Shader.PropertyToID("RTDI127590340.tiledLights");
        System.Int32 RTDI127590340_directionalLightBuffer = Shader.PropertyToID("RTDI127590340.directionalLightBuffer");
        System.Int32 RTDI127590340_depth = Shader.PropertyToID("RTDI127590340.depth");
        System.Int32 RTDI127590340_diffuse = Shader.PropertyToID("RTDI127590340.diffuse");
        System.Int32 RTDI127590340_specular = Shader.PropertyToID("RTDI127590340.specular");
        System.Int32 RTDI127590340_normal = Shader.PropertyToID("RTDI127590340.normal");
        System.Int32 RTDI127590340_emission = Shader.PropertyToID("RTDI127590340.emission");
        System.Int32 RTDI127590340_ao = Shader.PropertyToID("RTDI127590340.ao");
        System.Int32 RTDI127590340_lightingResult = Shader.PropertyToID("RTDI127590340.lightingResult");
        System.Int32 CombineColorDepth760392706_color = Shader.PropertyToID("CombineColorDepth760392706.color");
        System.Int32 CombineColorDepth760392706_depth = Shader.PropertyToID("CombineColorDepth760392706.depth");
        System.Int32 CombineColorDepth760392706_combined = Shader.PropertyToID("CombineColorDepth760392706.combined");
        System.Int32 AreaLight145779702_lights = Shader.PropertyToID("AreaLight145779702.lights");
        System.Int32 AreaLight145779702_target = Shader.PropertyToID("AreaLight145779702.target");
        System.Int32 AreaLight145779702_depth = Shader.PropertyToID("AreaLight145779702.depth");
        System.Int32 AreaLight145779702_diffuse = Shader.PropertyToID("AreaLight145779702.diffuse");
        System.Int32 AreaLight145779702_specular = Shader.PropertyToID("AreaLight145779702.specular");
        System.Int32 AreaLight145779702_normal = Shader.PropertyToID("AreaLight145779702.normal");
        System.Int32 AreaLight145779702_ao = Shader.PropertyToID("AreaLight145779702.ao");
        System.Int32 SunAtmo970969601_sunLight = Shader.PropertyToID("SunAtmo970969601.sunLight");
        System.Int32 SunAtmo970969601_target = Shader.PropertyToID("SunAtmo970969601.target");
        System.Int32 SunAtmo970969601_depth = Shader.PropertyToID("SunAtmo970969601.depth");
        System.Int32 SunAtmo970969601_diffuse = Shader.PropertyToID("SunAtmo970969601.diffuse");
        System.Int32 SunAtmo970969601_specular = Shader.PropertyToID("SunAtmo970969601.specular");
        System.Int32 SunAtmo970969601_normal = Shader.PropertyToID("SunAtmo970969601.normal");
        System.Int32 SunAtmo970969601_ao = Shader.PropertyToID("SunAtmo970969601.ao");
        System.Int32 SunAtmo970969601_skyBox = Shader.PropertyToID("SunAtmo970969601.skyBox");
        System.Int32 SunAtmo970969601_sunBuffer = Shader.PropertyToID("SunAtmo970969601.sunBuffer");
        System.Int32 SkyBoxIBL669583313_skyBox = Shader.PropertyToID("SkyBoxIBL669583313.skyBox");
        System.Int32 SkyBoxIBL669583313_target = Shader.PropertyToID("SkyBoxIBL669583313.target");
        System.Int32 SkyBoxIBL669583313_depth = Shader.PropertyToID("SkyBoxIBL669583313.depth");
        System.Int32 SkyBoxIBL669583313_diffuse = Shader.PropertyToID("SkyBoxIBL669583313.diffuse");
        System.Int32 SkyBoxIBL669583313_specular = Shader.PropertyToID("SkyBoxIBL669583313.specular");
        System.Int32 SkyBoxIBL669583313_normal = Shader.PropertyToID("SkyBoxIBL669583313.normal");
        System.Int32 SkyBoxIBL669583313_ao = Shader.PropertyToID("SkyBoxIBL669583313.ao");
        System.Int32 SkyBoxIBL669583313_irradiance = Shader.PropertyToID("SkyBoxIBL669583313.irradiance");
        System.Int32 HiDepth573432727_depth = Shader.PropertyToID("HiDepth573432727.depth");
        System.Int32 HiDepth573432727_hiZ = Shader.PropertyToID("HiDepth573432727.hiZ");
        System.Int32 RTSpecular673834354_lightBuffer = Shader.PropertyToID("RTSpecular673834354.lightBuffer");
        System.Int32 RTSpecular673834354_tiledLights = Shader.PropertyToID("RTSpecular673834354.tiledLights");
        System.Int32 RTSpecular673834354_lights = Shader.PropertyToID("RTSpecular673834354.lights");
        System.Int32 RTSpecular673834354_sun = Shader.PropertyToID("RTSpecular673834354.sun");
        System.Int32 RTSpecular673834354_sceneColor = Shader.PropertyToID("RTSpecular673834354.sceneColor");
        System.Int32 RTSpecular673834354_filteredColor = Shader.PropertyToID("RTSpecular673834354.filteredColor");
        System.Int32 RTSpecular673834354_hiZ = Shader.PropertyToID("RTSpecular673834354.hiZ");
        System.Int32 RTSpecular673834354_motion = Shader.PropertyToID("RTSpecular673834354.motion");
        System.Int32 RTSpecular673834354_specular = Shader.PropertyToID("RTSpecular673834354.specular");
        System.Int32 RTSpecular673834354_normal = Shader.PropertyToID("RTSpecular673834354.normal");
        System.Int32 RTSpecular673834354_ao = Shader.PropertyToID("RTSpecular673834354.ao");
        System.Int32 RTSpecular673834354_skybox = Shader.PropertyToID("RTSpecular673834354.skybox");
        System.Int32 RTSpecular673834354_result = Shader.PropertyToID("RTSpecular673834354.result");
        System.Int32 PyramidColor490550593_filterTarget = Shader.PropertyToID("PyramidColor490550593.filterTarget");
        System.Int32 PyramidColor490550593_pyramidColor = Shader.PropertyToID("PyramidColor490550593.pyramidColor");
        System.Int32 Transparent230075522_lightBuffer = Shader.PropertyToID("Transparent230075522.lightBuffer");
        System.Int32 Transparent230075522_tiledLights = Shader.PropertyToID("Transparent230075522.tiledLights");
        System.Int32 Transparent230075522_lights = Shader.PropertyToID("Transparent230075522.lights");
        System.Int32 Transparent230075522_areaLightBuffer = Shader.PropertyToID("Transparent230075522.areaLightBuffer");
        System.Int32 Transparent230075522_filterdScreenColor = Shader.PropertyToID("Transparent230075522.filterdScreenColor");
        System.Int32 Transparent230075522_target = Shader.PropertyToID("Transparent230075522.target");
        System.Int32 Transparent230075522_depth = Shader.PropertyToID("Transparent230075522.depth");
        System.Int32 RTGI70063463_lightBuffer = Shader.PropertyToID("RTGI70063463.lightBuffer");
        System.Int32 RTGI70063463_tiledLights = Shader.PropertyToID("RTGI70063463.tiledLights");
        System.Int32 RTGI70063463_lights = Shader.PropertyToID("RTGI70063463.lights");
        System.Int32 RTGI70063463_sun = Shader.PropertyToID("RTGI70063463.sun");
        System.Int32 RTGI70063463_target = Shader.PropertyToID("RTGI70063463.target");
        System.Int32 RTGI70063463_depth = Shader.PropertyToID("RTGI70063463.depth");
        System.Int32 RTGI70063463_motion = Shader.PropertyToID("RTGI70063463.motion");
        System.Int32 RTGI70063463_diffuse = Shader.PropertyToID("RTGI70063463.diffuse");
        System.Int32 RTGI70063463_normal = Shader.PropertyToID("RTGI70063463.normal");
        System.Int32 RTGI70063463_skybox = Shader.PropertyToID("RTGI70063463.skybox");
        System.Int32 PyramidColor625127020_filterTarget = Shader.PropertyToID("PyramidColor625127020.filterTarget");
        System.Int32 PyramidColor625127020_pyramidColor = Shader.PropertyToID("PyramidColor625127020.pyramidColor");
        // ----------------------------

        public void Init()
        {
            GBuffer290612621 = new GBuffer();
            GBuffer290612621.a = 1f;
            GBuffer290612621.b = false;
            GBuffer290612621.c = new Vector2(1.10973f, 2f);
            GBuffer290612621.d = E.d;
            GBuffer290612621.e = 10;
            GBuffer290612621.f = new Color(1f, 2f, 3f, 0.22334f);
            GBuffer290612621.enabled = true;
            OutputNode602049860 = new OutputNode();
            OutputNode602049860.enabled = true;
            CullLight446935086 = new CullLight();
            CullLight446935086.cullingType = CullLight.CullingType.Frustum;
            CullLight446935086.radius = 200f;
            CullLight446935086.faraway = 100f;
            CullLight446935086.enabled = true;
            ZBin949438803 = new ZBin();
            ZBin949438803.tileCount = new Vector3Int(128, 64, 24);
            ZBin949438803.maxLightCountPerTile = 64;
            ZBin949438803.includeRTLight = true;
            ZBin949438803.enabled = true;
            RTDI127590340 = new RTDI();
            RTDI127590340.includeSunlight = false;
            RTDI127590340.enabled = true;
            CombineColorDepth760392706 = new CombineColorDepth();
            CombineColorDepth760392706.enabled = true;
            AreaLight145779702 = new AreaLight();
            AreaLight145779702.enabled = true;
            SunAtmo970969601 = new SunAtmo();
            SunAtmo970969601.TLutResolution = new Vector2Int(128, 128);
            SunAtmo970969601.SkyLutResolution = new Vector2Int(64, 224);
            SunAtmo970969601.MSLutResolution = new Vector2Int(32, 32);
            SunAtmo970969601.VolumeResolution = new Vector3Int(32, 32, 32);
            SunAtmo970969601.VolumeMaxDepth = 32000f;
            SunAtmo970969601.enabled = true;
            SkyBoxIBL669583313 = new SkyBoxIBL();
            SkyBoxIBL669583313.enabled = false;
            HiDepth573432727 = new HiDepth();
            HiDepth573432727.enabled = true;
            RTSpecular673834354 = new RTSpecular();
            RTSpecular673834354.useRTShadow = true;
            RTSpecular673834354.enabled = true;
            PyramidColor490550593 = new PyramidColor();
            PyramidColor490550593.enabled = true;
            Transparent230075522 = new Transparent();
            Transparent230075522.renderAreaLight = true;
            Transparent230075522.enabled = true;
            RTGI70063463 = new RTGI();
            RTGI70063463.useRTShadow = true;
            RTGI70063463.maxDepth = 2;
            RTGI70063463.spp = 1;
            RTGI70063463.showVariance = false;
            RTGI70063463.enabled = true;
            PyramidColor625127020 = new PyramidColor();
            PyramidColor625127020.enabled = true;
        }

        public int Execute(RenderContext context, bool debug = false)
        {
            System.Int32 result = -1;

            //GBuffer290612621
            {
                // preprocess node
                {
                    // inputs
                    GBuffer290612621.depth.AllocateResourcces(context, GBuffer290612621_depth);
                    GBuffer290612621.depth.name = "GBuffer290612621.depth";

                    // outputs
                    GBuffer290612621.diffuse.AllocateResourcces(context, GBuffer290612621_diffuse);
                    GBuffer290612621.diffuse.name = "GBuffer290612621.diffuse";
                    GBuffer290612621.diffuse.connected = true;
                    GBuffer290612621.specular.AllocateResourcces(context, GBuffer290612621_specular);
                    GBuffer290612621.specular.name = "GBuffer290612621.specular";
                    GBuffer290612621.specular.connected = true;
                    GBuffer290612621.normal.AllocateResourcces(context, GBuffer290612621_normal);
                    GBuffer290612621.normal.name = "GBuffer290612621.normal";
                    GBuffer290612621.normal.connected = true;
                    GBuffer290612621.emission.AllocateResourcces(context, GBuffer290612621_emission);
                    GBuffer290612621.emission.name = "GBuffer290612621.emission";
                    GBuffer290612621.emission.connected = true;
                    GBuffer290612621.microAO.AllocateResourcces(context, GBuffer290612621_microAO);
                    GBuffer290612621.microAO.name = "GBuffer290612621.microAO";
                    GBuffer290612621.microAO.connected = true;
                    GBuffer290612621.motion.AllocateResourcces(context, GBuffer290612621_motion);
                    GBuffer290612621.motion.name = "GBuffer290612621.motion";
                    GBuffer290612621.motion.connected = true;
                    context.commandBuffer.name = "GBuffer290612621 Pre";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // perform node
                {
                    context.commandBuffer.name = "GBuffer290612621";
                    GBuffer290612621.Excute(context);
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // postprocess node
                {
                }
            }
            //CullLight446935086
            {
                // preprocess node
                {
                    // inputs
                    // outputs
                    CullLight446935086.lights.AllocateResourcces(context, CullLight446935086_lights);
                    CullLight446935086.lights.name = "CullLight446935086.lights";
                    CullLight446935086.lights.connected = true;
                    context.commandBuffer.name = "CullLight446935086 Pre";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // perform node
                {
                    context.commandBuffer.name = "CullLight446935086";
                    CullLight446935086.Excute(context);
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // postprocess node
                {
                }
            }
            //ZBin949438803
            {
                // preprocess node
                {
                    // inputs
                    ZBin949438803.lights.connected = true;
                    ZBin949438803.lights.Move(CullLight446935086.lights);

                    ZBin949438803.depth.AllocateResourcces(context, ZBin949438803_depth);
                    ZBin949438803.depth.name = "ZBin949438803.depth";

                    // outputs
                    ZBin949438803.lightBuffer.AllocateResourcces(context, ZBin949438803_lightBuffer);
                    ZBin949438803.lightBuffer.name = "ZBin949438803.lightBuffer";
                    ZBin949438803.lightBuffer.connected = true;
                    ZBin949438803.tileLights.AllocateResourcces(context, ZBin949438803_tileLights);
                    ZBin949438803.tileLights.name = "ZBin949438803.tileLights";
                    ZBin949438803.tileLights.connected = true;
                    context.commandBuffer.name = "ZBin949438803 Pre";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // perform node
                {
                    context.commandBuffer.name = "ZBin949438803";
                    ZBin949438803.Excute(context);
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // postprocess node
                {
                    ZBin949438803.depth.ReleaseResourcces(context);
                    context.commandBuffer.name = "ZBin949438803 Post";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
            }
            //RTDI127590340
            {
                // preprocess node
                {
                    // inputs
                    RTDI127590340.lights.connected = true;
                    RTDI127590340.lights.Move(CullLight446935086.lights);

                    RTDI127590340.lightBuffer.connected = true;
                    RTDI127590340.lightBuffer.Move(ZBin949438803.lightBuffer);

                    RTDI127590340.tiledLights.connected = true;
                    RTDI127590340.tiledLights.Move(ZBin949438803.tileLights);

                    RTDI127590340.directionalLightBuffer.AllocateResourcces(context, RTDI127590340_directionalLightBuffer);
                    RTDI127590340.directionalLightBuffer.name = "RTDI127590340.directionalLightBuffer";

                    RTDI127590340.depth.connected = true;
                    RTDI127590340.depth.Move(GBuffer290612621.depth);

                    RTDI127590340.diffuse.connected = true;
                    RTDI127590340.diffuse.Move(GBuffer290612621.diffuse);

                    RTDI127590340.specular.connected = true;
                    RTDI127590340.specular.Move(GBuffer290612621.specular);

                    RTDI127590340.normal.connected = true;
                    RTDI127590340.normal.Move(GBuffer290612621.normal);

                    RTDI127590340.emission.connected = true;
                    RTDI127590340.emission.Move(GBuffer290612621.emission);

                    RTDI127590340.ao.connected = true;
                    RTDI127590340.ao.Move(GBuffer290612621.microAO);

                    // outputs
                    RTDI127590340.lightingResult.AllocateResourcces(context, RTDI127590340_lightingResult);
                    RTDI127590340.lightingResult.name = "RTDI127590340.lightingResult";
                    RTDI127590340.lightingResult.connected = true;
                    context.commandBuffer.name = "RTDI127590340 Pre";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // perform node
                {
                    context.commandBuffer.name = "RTDI127590340";
                    RTDI127590340.Excute(context);
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // postprocess node
                {
                    RTDI127590340.directionalLightBuffer.ReleaseResourcces(context);
                    RTDI127590340.emission.ReleaseResourcces(context);
                    context.commandBuffer.name = "RTDI127590340 Post";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
            }
            //AreaLight145779702
            {
                // preprocess node
                {
                    // inputs
                    AreaLight145779702.lights.connected = true;
                    AreaLight145779702.lights.Move(CullLight446935086.lights);

                    AreaLight145779702.target.connected = true;
                    AreaLight145779702.target.Move(RTDI127590340.lightingResult);

                    AreaLight145779702.depth.connected = true;
                    AreaLight145779702.depth.AllocateResourcces(context, AreaLight145779702_depth);
                    AreaLight145779702.depth.name = "AreaLight145779702.depth";
                    AreaLight145779702.depth.CastFrom(context, GBuffer290612621.depth);

                    AreaLight145779702.diffuse.connected = true;
                    AreaLight145779702.diffuse.Move(GBuffer290612621.diffuse);

                    AreaLight145779702.specular.connected = true;
                    AreaLight145779702.specular.Move(GBuffer290612621.specular);

                    AreaLight145779702.normal.connected = true;
                    AreaLight145779702.normal.Move(GBuffer290612621.normal);

                    AreaLight145779702.ao.connected = true;
                    AreaLight145779702.ao.Move(GBuffer290612621.microAO);

                    // outputs
                    context.commandBuffer.name = "AreaLight145779702 Pre";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // perform node
                {
                    context.commandBuffer.name = "AreaLight145779702";
                    AreaLight145779702.Excute(context);
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // postprocess node
                {
                }
            }
            //HiDepth573432727
            {
                // preprocess node
                {
                    // inputs
                    HiDepth573432727.depth.connected = true;
                    HiDepth573432727.depth.Move(AreaLight145779702.depth);

                    // outputs
                    HiDepth573432727.hiZ.AllocateResourcces(context, HiDepth573432727_hiZ);
                    HiDepth573432727.hiZ.name = "HiDepth573432727.hiZ";
                    HiDepth573432727.hiZ.connected = true;
                    context.commandBuffer.name = "HiDepth573432727 Pre";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // perform node
                {
                    context.commandBuffer.name = "HiDepth573432727";
                    HiDepth573432727.Excute(context);
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // postprocess node
                {
                }
            }
            //SunAtmo970969601
            {
                // preprocess node
                {
                    // inputs
                    SunAtmo970969601.sunLight.connected = true;
                    SunAtmo970969601.sunLight.Move(CullLight446935086.lights);

                    SunAtmo970969601.target.connected = true;
                    SunAtmo970969601.target.Move(AreaLight145779702.target);

                    SunAtmo970969601.depth.connected = true;
                    SunAtmo970969601.depth.Move(AreaLight145779702.depth);

                    SunAtmo970969601.diffuse.connected = true;
                    SunAtmo970969601.diffuse.Move(GBuffer290612621.diffuse);

                    SunAtmo970969601.specular.connected = true;
                    SunAtmo970969601.specular.Move(GBuffer290612621.specular);

                    SunAtmo970969601.normal.connected = true;
                    SunAtmo970969601.normal.Move(GBuffer290612621.normal);

                    SunAtmo970969601.ao.connected = true;
                    SunAtmo970969601.ao.Move(GBuffer290612621.microAO);

                    // outputs
                    SunAtmo970969601.skyBox.AllocateResourcces(context, SunAtmo970969601_skyBox);
                    SunAtmo970969601.skyBox.name = "SunAtmo970969601.skyBox";
                    SunAtmo970969601.skyBox.connected = true;
                    SunAtmo970969601.sunBuffer.AllocateResourcces(context, SunAtmo970969601_sunBuffer);
                    SunAtmo970969601.sunBuffer.name = "SunAtmo970969601.sunBuffer";
                    SunAtmo970969601.sunBuffer.connected = true;
                    context.commandBuffer.name = "SunAtmo970969601 Pre";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // perform node
                {
                    context.commandBuffer.name = "SunAtmo970969601";
                    SunAtmo970969601.Excute(context);
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // postprocess node
                {
                }
            }
            //SkyBoxIBL669583313
            {
                // preprocess node
                {
                    // inputs
                    SkyBoxIBL669583313.skyBox.connected = true;
                    SkyBoxIBL669583313.skyBox.Move(SunAtmo970969601.skyBox);

                    SkyBoxIBL669583313.target.connected = true;
                    SkyBoxIBL669583313.target.Move(SunAtmo970969601.target);

                    SkyBoxIBL669583313.depth.connected = true;
                    SkyBoxIBL669583313.depth.Move(AreaLight145779702.depth);

                    SkyBoxIBL669583313.diffuse.connected = true;
                    SkyBoxIBL669583313.diffuse.Move(GBuffer290612621.diffuse);

                    SkyBoxIBL669583313.specular.connected = true;
                    SkyBoxIBL669583313.specular.Move(GBuffer290612621.specular);

                    SkyBoxIBL669583313.normal.connected = true;
                    SkyBoxIBL669583313.normal.Move(GBuffer290612621.normal);

                    SkyBoxIBL669583313.ao.connected = true;
                    SkyBoxIBL669583313.ao.Move(GBuffer290612621.microAO);

                    // outputs
                    SkyBoxIBL669583313.irradiance.AllocateResourcces(context, SkyBoxIBL669583313_irradiance);
                    SkyBoxIBL669583313.irradiance.name = "SkyBoxIBL669583313.irradiance";
                    SkyBoxIBL669583313.irradiance.connected = false;
                    context.commandBuffer.name = "SkyBoxIBL669583313 Pre";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // perform node
                {
                }
                // postprocess node
                {
                    SkyBoxIBL669583313.irradiance.ReleaseResourcces(context);
                    context.commandBuffer.name = "SkyBoxIBL669583313 Post";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
            }
            //RTGI70063463
            {
                // preprocess node
                {
                    // inputs
                    RTGI70063463.lightBuffer.connected = true;
                    RTGI70063463.lightBuffer.Move(ZBin949438803.lightBuffer);

                    RTGI70063463.tiledLights.connected = true;
                    RTGI70063463.tiledLights.Move(ZBin949438803.tileLights);

                    RTGI70063463.lights.connected = true;
                    RTGI70063463.lights.Move(CullLight446935086.lights);

                    RTGI70063463.sun.connected = true;
                    RTGI70063463.sun.Move(SunAtmo970969601.sunBuffer);

                    RTGI70063463.target.connected = false;
                    RTGI70063463.target.Move(SkyBoxIBL669583313.target);

                    RTGI70063463.depth.connected = true;
                    RTGI70063463.depth.Move(AreaLight145779702.depth);

                    RTGI70063463.motion.connected = true;
                    RTGI70063463.motion.Move(GBuffer290612621.motion);

                    RTGI70063463.diffuse.connected = true;
                    RTGI70063463.diffuse.Move(GBuffer290612621.diffuse);

                    RTGI70063463.normal.connected = true;
                    RTGI70063463.normal.Move(GBuffer290612621.normal);

                    RTGI70063463.skybox.connected = true;
                    RTGI70063463.skybox.Move(SunAtmo970969601.skyBox);

                    // outputs
                }
                // perform node
                {
                    context.commandBuffer.name = "RTGI70063463";
                    RTGI70063463.Excute(context);
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // postprocess node
                {
                }
            }
            //PyramidColor625127020
            {
                // preprocess node
                {
                    // inputs
                    PyramidColor625127020.filterTarget.connected = true;
                    PyramidColor625127020.filterTarget.Move(RTGI70063463.target);

                    // outputs
                    PyramidColor625127020.pyramidColor.AllocateResourcces(context, PyramidColor625127020_pyramidColor);
                    PyramidColor625127020.pyramidColor.name = "PyramidColor625127020.pyramidColor";
                    PyramidColor625127020.pyramidColor.connected = true;
                    context.commandBuffer.name = "PyramidColor625127020 Pre";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // perform node
                {
                    context.commandBuffer.name = "PyramidColor625127020";
                    PyramidColor625127020.Excute(context);
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // postprocess node
                {
                }
            }
            //RTSpecular673834354
            {
                // preprocess node
                {
                    // inputs
                    RTSpecular673834354.lightBuffer.connected = true;
                    RTSpecular673834354.lightBuffer.Move(ZBin949438803.lightBuffer);

                    RTSpecular673834354.tiledLights.connected = true;
                    RTSpecular673834354.tiledLights.Move(ZBin949438803.tileLights);

                    RTSpecular673834354.lights.connected = true;
                    RTSpecular673834354.lights.Move(CullLight446935086.lights);

                    RTSpecular673834354.sun.connected = true;
                    RTSpecular673834354.sun.Move(SunAtmo970969601.sunBuffer);

                    RTSpecular673834354.sceneColor.connected = true;
                    RTSpecular673834354.sceneColor.Move(RTGI70063463.target);

                    RTSpecular673834354.filteredColor.connected = true;
                    RTSpecular673834354.filteredColor.Move(PyramidColor625127020.pyramidColor);

                    RTSpecular673834354.hiZ.connected = true;
                    RTSpecular673834354.hiZ.Move(HiDepth573432727.hiZ);

                    RTSpecular673834354.motion.connected = true;
                    RTSpecular673834354.motion.Move(GBuffer290612621.motion);

                    RTSpecular673834354.specular.connected = true;
                    RTSpecular673834354.specular.Move(GBuffer290612621.specular);

                    RTSpecular673834354.normal.connected = true;
                    RTSpecular673834354.normal.Move(GBuffer290612621.normal);

                    RTSpecular673834354.ao.connected = true;
                    RTSpecular673834354.ao.Move(GBuffer290612621.microAO);

                    RTSpecular673834354.skybox.connected = true;
                    RTSpecular673834354.skybox.Move(SunAtmo970969601.skyBox);

                    // outputs
                    RTSpecular673834354.result.AllocateResourcces(context, RTSpecular673834354_result);
                    RTSpecular673834354.result.name = "RTSpecular673834354.result";
                    RTSpecular673834354.result.connected = true;
                    context.commandBuffer.name = "RTSpecular673834354 Pre";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // perform node
                {
                    context.commandBuffer.name = "RTSpecular673834354";
                    RTSpecular673834354.Excute(context);
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // postprocess node
                {
                    RTSpecular673834354.filteredColor.ReleaseResourcces(context);
                    RTSpecular673834354.hiZ.ReleaseResourcces(context);
                    context.commandBuffer.name = "RTSpecular673834354 Post";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
            }
            //PyramidColor490550593
            {
                // preprocess node
                {
                    // inputs
                    PyramidColor490550593.filterTarget.connected = true;
                    PyramidColor490550593.filterTarget.Move(RTSpecular673834354.result);

                    // outputs
                    PyramidColor490550593.pyramidColor.AllocateResourcces(context, PyramidColor490550593_pyramidColor);
                    PyramidColor490550593.pyramidColor.name = "PyramidColor490550593.pyramidColor";
                    PyramidColor490550593.pyramidColor.connected = true;
                    context.commandBuffer.name = "PyramidColor490550593 Pre";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // perform node
                {
                    context.commandBuffer.name = "PyramidColor490550593";
                    PyramidColor490550593.Excute(context);
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // postprocess node
                {
                }
            }
            //Transparent230075522
            {
                // preprocess node
                {
                    // inputs
                    Transparent230075522.lightBuffer.connected = true;
                    Transparent230075522.lightBuffer.Move(ZBin949438803.lightBuffer);

                    Transparent230075522.tiledLights.connected = true;
                    Transparent230075522.tiledLights.Move(ZBin949438803.tileLights);

                    Transparent230075522.lights.connected = true;
                    Transparent230075522.lights.Move(CullLight446935086.lights);

                    Transparent230075522.areaLightBuffer.AllocateResourcces(context, Transparent230075522_areaLightBuffer);
                    Transparent230075522.areaLightBuffer.name = "Transparent230075522.areaLightBuffer";

                    Transparent230075522.filterdScreenColor.connected = true;
                    Transparent230075522.filterdScreenColor.Move(PyramidColor490550593.pyramidColor);

                    Transparent230075522.target.connected = true;
                    Transparent230075522.target.AllocateResourcces(context, Transparent230075522_target);
                    Transparent230075522.target.name = "Transparent230075522.target";
                    Transparent230075522.target.CastFrom(context, RTSpecular673834354.result);

                    Transparent230075522.depth.connected = true;
                    Transparent230075522.depth.Move(AreaLight145779702.depth);

                    // outputs
                    context.commandBuffer.name = "Transparent230075522 Pre";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // perform node
                {
                    context.commandBuffer.name = "Transparent230075522";
                    Transparent230075522.Excute(context);
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // postprocess node
                {
                    Transparent230075522.areaLightBuffer.ReleaseResourcces(context);
                    Transparent230075522.filterdScreenColor.ReleaseResourcces(context);
                    context.commandBuffer.name = "Transparent230075522 Post";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
            }
            //CombineColorDepth760392706
            {
                // preprocess node
                {
                    // inputs
                    CombineColorDepth760392706.color.connected = true;
                    CombineColorDepth760392706.color.Move(Transparent230075522.target);

                    CombineColorDepth760392706.depth.connected = true;
                    CombineColorDepth760392706.depth.Move(GBuffer290612621.depth);

                    // outputs
                    CombineColorDepth760392706.combined.AllocateResourcces(context, CombineColorDepth760392706_combined);
                    CombineColorDepth760392706.combined.name = "CombineColorDepth760392706.combined";
                    CombineColorDepth760392706.combined.connected = true;
                    context.commandBuffer.name = "CombineColorDepth760392706 Pre";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // perform node
                {
                    context.commandBuffer.name = "CombineColorDepth760392706";
                    CombineColorDepth760392706.Excute(context);
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // postprocess node
                {
                    CombineColorDepth760392706.color.ReleaseResourcces(context);
                    context.commandBuffer.name = "CombineColorDepth760392706 Post";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
            }
            //OutputNode602049860
            {
                // preprocess node
                {
                    // inputs
                    OutputNode602049860.result.connected = true;
                    OutputNode602049860.result.AllocateResourcces(context, OutputNode602049860_result);
                    OutputNode602049860.result.name = "OutputNode602049860.result";
                    OutputNode602049860.result.CastFrom(context, CombineColorDepth760392706.combined);
                    CombineColorDepth760392706.combined.ReleaseResourcces(context);

                    // outputs
                    context.commandBuffer.name = "OutputNode602049860 Pre";
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    context.commandBuffer.Clear();
                }
                // perform node
                {
                    result = OutputNode602049860.result.handle;
                    return result;
                }
            }
        }

        public void Dispose()
        {
            // GBuffer290612621
            {
                GBuffer290612621.depth.Dispose();
                GBuffer290612621.diffuse.Dispose();
                GBuffer290612621.specular.Dispose();
                GBuffer290612621.normal.Dispose();
                GBuffer290612621.emission.Dispose();
                GBuffer290612621.microAO.Dispose();
                GBuffer290612621.motion.Dispose();
                GBuffer290612621.Dispose();
            }
            // OutputNode602049860
            {
                OutputNode602049860.result.Dispose();
                OutputNode602049860.Dispose();
            }
            // CullLight446935086
            {
                CullLight446935086.lights.Dispose();
                CullLight446935086.Dispose();
            }
            // ZBin949438803
            {
                ZBin949438803.lights.Dispose();
                ZBin949438803.depth.Dispose();
                ZBin949438803.lightBuffer.Dispose();
                ZBin949438803.tileLights.Dispose();
                ZBin949438803.Dispose();
            }
            // RTDI127590340
            {
                RTDI127590340.lights.Dispose();
                RTDI127590340.lightBuffer.Dispose();
                RTDI127590340.tiledLights.Dispose();
                RTDI127590340.directionalLightBuffer.Dispose();
                RTDI127590340.depth.Dispose();
                RTDI127590340.diffuse.Dispose();
                RTDI127590340.specular.Dispose();
                RTDI127590340.normal.Dispose();
                RTDI127590340.emission.Dispose();
                RTDI127590340.ao.Dispose();
                RTDI127590340.lightingResult.Dispose();
                RTDI127590340.Dispose();
            }
            // CombineColorDepth760392706
            {
                CombineColorDepth760392706.color.Dispose();
                CombineColorDepth760392706.depth.Dispose();
                CombineColorDepth760392706.combined.Dispose();
                CombineColorDepth760392706.Dispose();
            }
            // AreaLight145779702
            {
                AreaLight145779702.lights.Dispose();
                AreaLight145779702.target.Dispose();
                AreaLight145779702.depth.Dispose();
                AreaLight145779702.diffuse.Dispose();
                AreaLight145779702.specular.Dispose();
                AreaLight145779702.normal.Dispose();
                AreaLight145779702.ao.Dispose();
                AreaLight145779702.Dispose();
            }
            // SunAtmo970969601
            {
                SunAtmo970969601.sunLight.Dispose();
                SunAtmo970969601.target.Dispose();
                SunAtmo970969601.depth.Dispose();
                SunAtmo970969601.diffuse.Dispose();
                SunAtmo970969601.specular.Dispose();
                SunAtmo970969601.normal.Dispose();
                SunAtmo970969601.ao.Dispose();
                SunAtmo970969601.skyBox.Dispose();
                SunAtmo970969601.sunBuffer.Dispose();
                SunAtmo970969601.Dispose();
            }
            // SkyBoxIBL669583313
            {
                SkyBoxIBL669583313.skyBox.Dispose();
                SkyBoxIBL669583313.target.Dispose();
                SkyBoxIBL669583313.depth.Dispose();
                SkyBoxIBL669583313.diffuse.Dispose();
                SkyBoxIBL669583313.specular.Dispose();
                SkyBoxIBL669583313.normal.Dispose();
                SkyBoxIBL669583313.ao.Dispose();
                SkyBoxIBL669583313.irradiance.Dispose();
                SkyBoxIBL669583313.Dispose();
            }
            // HiDepth573432727
            {
                HiDepth573432727.depth.Dispose();
                HiDepth573432727.hiZ.Dispose();
                HiDepth573432727.Dispose();
            }
            // RTSpecular673834354
            {
                RTSpecular673834354.lightBuffer.Dispose();
                RTSpecular673834354.tiledLights.Dispose();
                RTSpecular673834354.lights.Dispose();
                RTSpecular673834354.sun.Dispose();
                RTSpecular673834354.sceneColor.Dispose();
                RTSpecular673834354.filteredColor.Dispose();
                RTSpecular673834354.hiZ.Dispose();
                RTSpecular673834354.motion.Dispose();
                RTSpecular673834354.specular.Dispose();
                RTSpecular673834354.normal.Dispose();
                RTSpecular673834354.ao.Dispose();
                RTSpecular673834354.skybox.Dispose();
                RTSpecular673834354.result.Dispose();
                RTSpecular673834354.Dispose();
            }
            // PyramidColor490550593
            {
                PyramidColor490550593.filterTarget.Dispose();
                PyramidColor490550593.pyramidColor.Dispose();
                PyramidColor490550593.Dispose();
            }
            // Transparent230075522
            {
                Transparent230075522.lightBuffer.Dispose();
                Transparent230075522.tiledLights.Dispose();
                Transparent230075522.lights.Dispose();
                Transparent230075522.areaLightBuffer.Dispose();
                Transparent230075522.filterdScreenColor.Dispose();
                Transparent230075522.target.Dispose();
                Transparent230075522.depth.Dispose();
                Transparent230075522.Dispose();
            }
            // RTGI70063463
            {
                RTGI70063463.lightBuffer.Dispose();
                RTGI70063463.tiledLights.Dispose();
                RTGI70063463.lights.Dispose();
                RTGI70063463.sun.Dispose();
                RTGI70063463.target.Dispose();
                RTGI70063463.depth.Dispose();
                RTGI70063463.motion.Dispose();
                RTGI70063463.diffuse.Dispose();
                RTGI70063463.normal.Dispose();
                RTGI70063463.skybox.Dispose();
                RTGI70063463.Dispose();
            }
            // PyramidColor625127020
            {
                PyramidColor625127020.filterTarget.Dispose();
                PyramidColor625127020.pyramidColor.Dispose();
                PyramidColor625127020.Dispose();
            }
        }
    }

}