using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class TerrainPass : BaseRenderPass
    {
        [NodePin(PinType.In)]
        public LightListPin sunLight = new LightListPin();

        [NodePin(PinType.InOut)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                                        SizeCastMode.ResizeToInput,
                                                        ColorCastMode.Fixed,
                                                        SizeScale.Full);

        [NodePin(PinType.InOut)]
        public TexturePin diffuse = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                    SizeCastMode.Fixed,
                                                                    ColorCastMode.Fixed,
                                                                    SizeScale.Full);
        [NodePin(PinType.InOut)]
        public TexturePin specular = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                            SizeCastMode.Fixed,
                                                            ColorCastMode.Fixed,
                                                            SizeScale.Full);

        [NodePin(PinType.InOut)]
        public TexturePin normal = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                    SizeCastMode.Fixed,
                                                                    ColorCastMode.Fixed,
                                                                    SizeScale.Full);
        [NodePin(PinType.InOut)]
        public TexturePin microAO = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                    SizeCastMode.Fixed,
                                                                    ColorCastMode.Fixed,
                                                                    SizeScale.Full);

        [NodePin(PinType.Out)]
        public TexturePin terrainShadowMap = new TexturePin(new RenderTextureDescriptor(2048, 2048, RenderTextureFormat.RFloat, 0),
                                                                    SizeCastMode.Fixed,
                                                                    ColorCastMode.Fixed,
                                                                    SizeScale.Custom);

        public bool gameCameraCull = true;

        HRPTerrain terrain = null;

        public override void Execute(RenderContext context)
        {
            var cb = context.commandBuffer;

            if (!diffuse.connected)
            {
                cb.SetRenderTarget(diffuse);
                cb.ClearRenderTarget(false, true, Color.clear);
            }
            if (!specular.connected)
            {
                cb.SetRenderTarget(specular);
                cb.ClearRenderTarget(false, true, Color.clear);
            }
            if (!normal.connected)
            {
                cb.SetRenderTarget(normal);
                cb.ClearRenderTarget(false, true, Color.clear);
            }
            if (!microAO.connected)
            {
                cb.SetRenderTarget(microAO);
                cb.ClearRenderTarget(false, true, Color.clear);
            }

            cb.SetRenderTarget(new RenderTargetIdentifier[] { diffuse.handle, specular, normal, microAO }, depth: depth);
            cb.ClearRenderTarget(!depth.connected, false, Color.clear);

            var cam = gameCameraCull ? Camera.main ?? context.camera : context.camera;

            FrustumCulling.SetCullingCamera(context.commandBuffer, cam);

            if (terrain != null)
            {
                if (terrain.isActiveAndEnabled && terrain.cb != null)
                {
                    terrain.MoveTerrain(context.commandBuffer, cam);
                    cb.SetGlobalTexture("_TerrainHeight", terrain.terrainData.height[0]);
                    cb.SetGlobalVector("_HeightRange", terrain.terrainData.heightRange);
                    context.context.ExecuteCommandBuffer(cb);
                    cb.Clear();
                    context.context.ExecuteCommandBuffer(terrain.cb);

                    if (sunLight.handle.sunLight != null && terrainShadowMap.connected)
                    {
                        cb.SetRenderTarget(terrainShadowMap);
                        cb.ClearRenderTarget(false, true, Color.clear);

                        Transform strans = sunLight.handle.sunLight.transform;
                        Transform ctrans = cam.transform;
                        Matrix4x4 w2l = strans.worldToLocalMatrix;
                        Matrix4x4 l2w = strans.localToWorldMatrix;
                        Vector4 cpos = ctrans.position; cpos.w = 1;

                        Matrix4x4 p = (GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * cam.worldToCameraMatrix).inverse;
                        Vector4 lb = p * new Vector4(-1, -1, 0, 1); lb /= lb.w; lb = lb - cpos;
                        Vector4 lu = p * new Vector4(-1, 1, 0, 1); lu /= lu.w; lu = lu - cpos;
                        Vector4 rb = p * new Vector4(1, -1, 0, 1); rb /= rb.w; rb = rb - cpos;
                        Vector4 ru = p * new Vector4(1, 1, 0, 1); ru /= ru.w; ru = ru - cpos;

                        float cascade0 = 0.05f, cascade1 = 0.15f, cascade2 = 0.35f;

                        Matrix4x4 cm0, cm1, cm2, cm3;
                        // cascade 0
                        Vector4 c0min, c0max;
                        {
                            Vector4 a, b, c, d, e;
                            a = w2l * (lb * cascade0 + cpos);
                            b = w2l * (lu * cascade0 + cpos);
                            c = w2l * (rb * cascade0 + cpos);
                            d = w2l * (ru * cascade0 + cpos);
                            e = w2l * cpos;

                            c0max = Vector4.Max(Vector4.Max(a, b), Vector4.Max(c, d));
                            c0min = Vector4.Min(Vector4.Min(a, b), Vector4.Min(c, d));
                            Vector4 max = Vector4.Max(c0max, e);
                            Vector4 min = Vector4.Min(c0min, e);

                            min.z -= 500;
                            Vector3 scale = (max - min);
                            scale.z += 500;
                            cm0 = l2w * Matrix4x4.TRS(min, Quaternion.identity, scale);
                        }
                        // cascade 1
                        Vector4 c1min, c1max;
                        {
                            Vector4 a, b, c, d;
                            a = w2l * (lb * cascade1 + cpos);
                            b = w2l * (lu * cascade1 + cpos);
                            c = w2l * (rb * cascade1 + cpos);
                            d = w2l * (ru * cascade1 + cpos);

                            c1max = Vector4.Max(Vector4.Max(a, b), Vector4.Max(c, d));
                            c1min = Vector4.Min(Vector4.Min(a, b), Vector4.Min(c, d));
                            Vector4 max = Vector4.Max(c1max, c0max);
                            Vector4 min = Vector4.Min(c1min, c0min);

                            min.z -= 500;
                            Vector3 scale = (max - min);
                            scale.z += 500;
                            cm1 = l2w * Matrix4x4.TRS(min, Quaternion.identity, scale);
                        }
                        // cascade 2
                        Vector4 c2min, c2max;
                        {
                            Vector4 a, b, c, d;
                            a = w2l * (lb * cascade2 + cpos);
                            b = w2l * (lu * cascade2 + cpos);
                            c = w2l * (rb * cascade2 + cpos);
                            d = w2l * (ru * cascade2 + cpos);

                            c2max = Vector4.Max(Vector4.Max(a, b), Vector4.Max(c, d));
                            c2min = Vector4.Min(Vector4.Min(a, b), Vector4.Min(c, d));
                            Vector4 max = Vector4.Max(c2max, c1max);
                            Vector4 min = Vector4.Min(c2min, c1min);

                            min.z -= 500;
                            Vector3 scale = (max - min);
                            scale.z += 500;
                            cm2 = l2w * Matrix4x4.TRS(min, Quaternion.identity, scale);
                        }
                        // cascade 3
                        {
                            Vector4 a, b, c, d;
                            a = w2l * (lb + cpos);
                            b = w2l * (lu + cpos);
                            c = w2l * (rb + cpos);
                            d = w2l * (ru + cpos);

                            Vector4 c3min, c3max;
                            c3max = Vector4.Max(Vector4.Max(a, b), Vector4.Max(c, d));
                            c3min = Vector4.Min(Vector4.Min(a, b), Vector4.Min(c, d));
                            Vector4 max = Vector4.Max(c3max, c2max);
                            Vector4 min = Vector4.Min(c3min, c2min);

                            min.z -= 500;
                            Vector3 scale = (max - min);
                            scale.z += 500;
                            cm3 = l2w * Matrix4x4.TRS(min, Quaternion.identity, scale);
                        }

                        cb.SetGlobalMatrix("_TerrainShadowMatrix0", cm0);
                        cb.SetGlobalMatrix("_TerrainShadowMatrix1", cm1);
                        cb.SetGlobalMatrix("_TerrainShadowMatrix2", cm2);
                        cb.SetGlobalMatrix("_TerrainShadowMatrix3", cm3);

                        context.context.ExecuteCommandBuffer(cb);
                        cb.Clear();
                        context.context.ExecuteCommandBuffer(terrain.shadowCb);

                        cb.SetGlobalMatrix("_TerrainShadowMatrix0", cm0.inverse);
                        cb.SetGlobalMatrix("_TerrainShadowMatrix1", cm1.inverse);
                        cb.SetGlobalMatrix("_TerrainShadowMatrix2", cm2.inverse);
                        cb.SetGlobalMatrix("_TerrainShadowMatrix3", cm3.inverse);
                        cb.SetGlobalTexture("_TerrainShadowMap", terrainShadowMap);
                    }
                }
            }
            else
            {
                terrain = GameObject.FindObjectOfType<HRPTerrain>();
            }

            cb.SetGlobalTexture("_DepthTex", depth);
            cb.SetGlobalTexture("_BaseColorTex", diffuse);
            cb.SetGlobalTexture("_SpecTex", specular);
            cb.SetGlobalTexture("_NormalTex", normal);
            cb.SetGlobalTexture("_AOTex", microAO);
        }
    }
}