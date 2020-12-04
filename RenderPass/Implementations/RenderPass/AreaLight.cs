using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline.RenderPass
{

    public class AreaLight : BaseRenderPass
    {
        [NodePin(PinType.In, true)]
        public LightListPin lights = new LightListPin();

        [NodePin]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.DefaultHDR, 0));

        [NodePin(PinType.InOut, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        [NodePin(PinType.In, true)]
        public TexturePin diffuse = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin specular = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin normal = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In)]
        public TexturePin ao = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        static MaterialWithName lightMat = new MaterialWithName("Hidden/LightMesh");
        static MaterialWithName clearAlphaMat = new MaterialWithName("Hidden/ClearAlpha");
        static MaterialWithName deferredLightingMat = new MaterialWithName("Hidden/DeferredLighting");

        static Texture2D TransformInv_Diffuse, TransformInv_Specular, AmpDiffAmpSpecFresnel, DiscClip;

        Mesh sphere, quad, tube;

        public AreaLight()
        {
            sphere = MeshWithType.sphere;
            tube = MeshWithType.cylinder;

            AmpDiffAmpSpecFresnel = Resources.Load<Texture2D>("Textures/LTC Lut/AmpDiffAmpSpecFresnel");
            TransformInv_Diffuse = Resources.Load<Texture2D>("Textures/LTC Lut/TransformInv_DisneyDiffuse");
            TransformInv_Specular = Resources.Load<Texture2D>("Textures/LTC Lut/TransformInv_GGX");
            DiscClip = Resources.Load<Texture2D>("Textures/LTC Lut/DiscClip");
        }

        public override void Execute(RenderContext context)
        {
            var cb = context.commandBuffer;

            if (quad == null)
            {
                quad = new Mesh();
                quad.name = "Quad";
                quad.vertices = new Vector3[] { float3(-0.5f, -0.5f, 0), float3(0.5f, -0.5f, 0), float3(-0.5f, 0.5f, 0), float3(0.5f, 0.5f, 0) };
                quad.triangles = new int[] { 0, 1, 2, 2, 1, 3 };
                quad.uv = new Vector2[] { float2(1, 0), float2(0, 0), float2(1, 1), float2(0, 1) };
            }

            if (!target.connected)
            {
                cb.SetRenderTarget(target);
                cb.ClearRenderTarget(false, true, Color.black);
            }

            cb.SetGlobalTexture("_AmpDiffAmpSpecFresnel", AmpDiffAmpSpecFresnel);
            cb.SetGlobalTexture("_TransformInv_Diffuse", TransformInv_Diffuse);
            cb.SetGlobalTexture("_TransformInv_Specular", TransformInv_Specular);
            cb.SetGlobalTexture("_DiscClip", DiscClip);

            foreach (var light in lights.handle.areas)
            {
                if (light.isArea)
                {
                    Mesh mesh = null;
                    Matrix4x4 mat = Matrix4x4.identity;
                    var trans = light.transform;
                    switch (light.lightType)
                    {
                        case HRPLightType.Sphere:
                            mat = Matrix4x4.TRS(trans.position, Quaternion.identity, Vector3.one * light.sphereRadius * 2);
                            mesh = sphere;
                            break;
                        case HRPLightType.Tube:
                            float2 lr = light.tubeLengthRadius;
                            lr.y *= 2;
                            mat = Matrix4x4.TRS(trans.position, trans.rotation * Quaternion.AngleAxis(90, Vector3.right), math.float3(lr.y, lr.x, lr.y));
                            mesh = tube;
                            break;
                        case HRPLightType.Quad:
                            mat = Matrix4x4.TRS(trans.position, trans.rotation, math.float3(light.quadSize, 1));
                            mesh = quad;
                            break;
                        case HRPLightType.Disc:
                            mat = Matrix4x4.TRS(trans.position, trans.rotation, math.float3(light.discRadius * 2));
                            mesh = quad;
                            break;
                        case HRPLightType.Mesh:
                            mesh = light.lightMesh;
                            mat = trans.localToWorldMatrix;
                            break;
                        default:
                            break;
                    }
                    if (mesh != null)
                    {
                        cb.SetGlobalColor("_LightColor", light.color * light.radiance);
                        if (light.drawLightMesh)
                        {
                            cb.SetGlobalTexture("_LightTex", (light.canHasTexture && light.areaTexture != null) ? light.areaTexture : Texture2D.whiteTexture);
                            cb.SetGlobalInt("_Disc", light.lightType == HRPLightType.Disc ? 1 : 0);
                            cb.SetRenderTarget(color: target, depth: depth);
                            cb.DrawMesh(mesh, mat, lightMat);
                        }

                        if (light.lightType == HRPLightType.Quad) // Current only support Quad light shading
                        {
                            var pos = light.transform.position;
                            var x = new Vector4(-trans.right.x, -trans.right.y, -trans.right.z, light.quadSize.x);
                            var y = new Vector4(trans.up.x, trans.up.y, trans.up.z, light.quadSize.y);
                            cb.SetGlobalVector("_LightPos", pos);
                            cb.SetGlobalVector("_LightX", x);
                            cb.SetGlobalVector("_LightY", y);

                            if (light.areaTexture != null)
                            {
                                cb.SetGlobalTexture("_LightDiffuseTex", light.filteredDiffuseTexture);
                                cb.SetGlobalTexture("_LightSpecTex", light.filteredSpecularTexture);
                            }
                            else
                            {
                                cb.SetGlobalTexture("_LightDiffuseTex", Texture2D.whiteTexture);
                                cb.SetGlobalTexture("_LightSpecTex", Texture2D.whiteTexture);
                            }

                            cb.Blit(null, target, deferredLightingMat, 1);
                        }
                        else if (light.lightType == HRPLightType.Tube)
                        {
                            var pos = light.transform.position;
                            var x = float4(trans.forward, light.tubeLengthRadius.x * 2);
                            var y = float4(trans.right, light.tubeLengthRadius.y * 2);
                            cb.SetGlobalVector("_LightPos", pos);
                            cb.SetGlobalVector("_LightX", x);
                            cb.SetGlobalVector("_LightY", y);

                            cb.SetGlobalTexture("_LightDiffuseTex", Texture2D.whiteTexture);
                            cb.SetGlobalTexture("_LightSpecTex", Texture2D.whiteTexture);

                            cb.Blit(null, target, deferredLightingMat, 2);
                        }
                        else if (light.lightType == HRPLightType.Disc)
                        {
                            var pos = light.transform.position;
                            var x = new Vector4(-trans.right.x, -trans.right.y, -trans.right.z, light.discRadius * 2);
                            var y = new Vector4(trans.up.x, trans.up.y, trans.up.z, light.discRadius * 2);
                            cb.SetGlobalVector("_LightPos", pos);
                            cb.SetGlobalVector("_LightX", x);
                            cb.SetGlobalVector("_LightY", y);

                            if (light.areaTexture != null)
                            {
                                cb.SetGlobalTexture("_LightDiffuseTex", light.filteredDiffuseTexture);
                                cb.SetGlobalTexture("_LightSpecTex", light.filteredSpecularTexture);
                            }
                            else
                            {
                                cb.SetGlobalTexture("_LightDiffuseTex", Texture2D.whiteTexture);
                                cb.SetGlobalTexture("_LightSpecTex", Texture2D.whiteTexture);
                            }

                            cb.Blit(null, target, deferredLightingMat, 3);
                        }
                    }
                }
            }
            cb.SetGlobalTexture("_DepthTex", depth);
        }
    }

}