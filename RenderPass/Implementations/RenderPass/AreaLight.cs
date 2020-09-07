using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

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
        public TexturePin baseColor_roughness = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In, true)]
        public TexturePin normal_metallic = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        [NodePin(PinType.In)]
        public TexturePin ao = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0));

        static MaterialWithName lightMat = new MaterialWithName("Hidden/LightMesh");
        static MaterialWithName clearAlphaMat = new MaterialWithName("Hidden/ClearAlpha");
        static MaterialWithName deferredLightingMat = new MaterialWithName("Hidden/DeferredLighting");

        Texture2D TransformInv_Diffuse, TransformInv_Specular, AmpDiffAmpSpecFresnel, DiscClip;

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

        public override void Excute(RenderContext context)
        {
            if (quad == null)
            {
                quad = new Mesh();
                quad.name = "Quad";
                quad.vertices = new Vector3[] { float3(-0.5f, -0.5f, 0), float3(0.5f, -0.5f, 0), float3(-0.5f, 0.5f, 0), float3(0.5f, 0.5f, 0) };
                quad.triangles = new int[] { 0, 1, 2, 2, 1, 3 };
                quad.uv = new Vector2[] { float2(1, 0), float2(0, 0), float2(1, 1), float2(0, 1) };
            }

            context.CmdBuffer.SetGlobalFloat("_Alpha", 1);
            if (!target.connected)
            {
                context.CmdBuffer.SetRenderTarget(target);
                context.CmdBuffer.ClearRenderTarget(false, true, Color.black);
            }
            else
            {
                // clear alpha before render area light mesh
                context.CmdBuffer.Blit(null, target, clearAlphaMat, 0);
            }

            context.CmdBuffer.SetGlobalTexture("_AmpDiffAmpSpecFresnel", AmpDiffAmpSpecFresnel);
            context.CmdBuffer.SetGlobalTexture("_TransformInv_Diffuse", TransformInv_Diffuse);
            context.CmdBuffer.SetGlobalTexture("_TransformInv_Specular", TransformInv_Specular);
            context.CmdBuffer.SetGlobalTexture("_DiscClip", DiscClip);

            context.CmdBuffer.SetGlobalTexture("_DepthTex", depth);
            context.CmdBuffer.SetGlobalTexture("_BaseColorTex", baseColor_roughness);
            context.CmdBuffer.SetGlobalTexture("_NormalTex", normal_metallic);
            if (ao.connected)
                context.CmdBuffer.SetGlobalTexture("_AOTex", ao);
            else
                context.CmdBuffer.SetGlobalTexture("_AOTex", Texture2D.whiteTexture);

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
                        context.CmdBuffer.SetGlobalColor("_LightColor", light.color * light.radiance);
                        if (light.drawLightMesh)
                        {
                            context.CmdBuffer.SetGlobalTexture("_LightTex", (light.canHasTexture && light.areaTexture != null) ? light.areaTexture : Texture2D.whiteTexture);
                            context.CmdBuffer.SetGlobalInt("_Disc", light.lightType == HRPLightType.Disc ? 1 : 0);
                            context.CmdBuffer.SetRenderTarget(color: target, depth: depth);
                            context.CmdBuffer.DrawMesh(mesh, mat, lightMat);
                        }

                        if (light.lightType == HRPLightType.Quad) // Current only support Quad light shading
                        {
                            var pos = light.transform.position;
                            var x = new Vector4(-trans.right.x, -trans.right.y, -trans.right.z, light.quadSize.x);
                            var y = new Vector4(trans.up.x, trans.up.y, trans.up.z, light.quadSize.y);
                            context.CmdBuffer.SetGlobalVector("_LightPos", pos);
                            context.CmdBuffer.SetGlobalVector("_LightX", x);
                            context.CmdBuffer.SetGlobalVector("_LightY", y);

                            if (light.areaTexture != null)
                            {
                                context.CmdBuffer.SetGlobalTexture("_LightDiffuseTex", light.filteredDiffuseTexture);
                                context.CmdBuffer.SetGlobalTexture("_LightSpecTex", light.filteredSpecularTexture);
                            }
                            else
                            {
                                context.CmdBuffer.SetGlobalTexture("_LightDiffuseTex", Texture2D.whiteTexture);
                                context.CmdBuffer.SetGlobalTexture("_LightSpecTex", Texture2D.whiteTexture);
                            }

                            context.CmdBuffer.Blit(null, target, deferredLightingMat, 1);
                        }
                        else if (light.lightType == HRPLightType.Tube)
                        {
                            var pos = light.transform.position;
                            var x = float4(trans.forward, light.tubeLengthRadius.x * 2);
                            var y = float4(trans.right, light.tubeLengthRadius.y * 2);
                            context.CmdBuffer.SetGlobalVector("_LightPos", pos);
                            context.CmdBuffer.SetGlobalVector("_LightX", x);
                            context.CmdBuffer.SetGlobalVector("_LightY", y);

                            context.CmdBuffer.SetGlobalTexture("_LightDiffuseTex", Texture2D.whiteTexture);
                            context.CmdBuffer.SetGlobalTexture("_LightSpecTex", Texture2D.whiteTexture);

                            context.CmdBuffer.Blit(null, target, deferredLightingMat, 2);
                        }
                        else if (light.lightType == HRPLightType.Disc)
                        {
                            var pos = light.transform.position;
                            var x = new Vector4(-trans.right.x, -trans.right.y, -trans.right.z, light.discRadius * 2);
                            var y = new Vector4(trans.up.x, trans.up.y, trans.up.z, light.discRadius * 2);
                            context.CmdBuffer.SetGlobalVector("_LightPos", pos);
                            context.CmdBuffer.SetGlobalVector("_LightX", x);
                            context.CmdBuffer.SetGlobalVector("_LightY", y);

                            if (light.areaTexture != null)
                            {
                                context.CmdBuffer.SetGlobalTexture("_LightDiffuseTex", light.filteredDiffuseTexture);
                                context.CmdBuffer.SetGlobalTexture("_LightSpecTex", light.filteredSpecularTexture);
                            }
                            else
                            {
                                context.CmdBuffer.SetGlobalTexture("_LightDiffuseTex", Texture2D.whiteTexture);
                                context.CmdBuffer.SetGlobalTexture("_LightSpecTex", Texture2D.whiteTexture);
                            }

                            context.CmdBuffer.Blit(null, target, deferredLightingMat, 3);
                        }
                    }
                }
            }
        }
    }

}