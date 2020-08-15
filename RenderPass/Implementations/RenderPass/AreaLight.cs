using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace HypnosRenderPipeline.RenderPass
{

    public class AreaLight : BaseRenderPass
    {

        [NodePin]
        public TexturePin target = new TexturePin(new TexturePinDesc(
                                                    new RenderTextureDescriptor(1, 1, RenderTextureFormat.DefaultHDR, 0),
                                                    TexturePinDesc.SizeCastMode.ResizeToInput,
                                                    TexturePinDesc.ColorCastMode.FitToInput,
                                                    TexturePinDesc.SizeScale.Full));

        [NodePin]
        public TexturePin depth = new TexturePin(new TexturePinDesc(
                                            new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                            TexturePinDesc.SizeCastMode.ResizeToInput,
                                            TexturePinDesc.ColorCastMode.Fixed,
                                            TexturePinDesc.SizeScale.Full));

        [NodePin(PinType.In, true)]
        public TexturePin baseColor_roughness = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                                       TexturePinDesc.SizeCastMode.ResizeToInput,
                                                                                       TexturePinDesc.ColorCastMode.FitToInput,
                                                                                       TexturePinDesc.SizeScale.Full));
        [NodePin(PinType.In, true)]
        public TexturePin normal_metallic = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                                       TexturePinDesc.SizeCastMode.ResizeToInput,
                                                                                       TexturePinDesc.ColorCastMode.FitToInput,
                                                                                       TexturePinDesc.SizeScale.Full));

        static MaterialWithName lightMat = new MaterialWithName("Hidden/LightMesh");
        static MaterialWithName deferredLightingMat = new MaterialWithName("Hidden/DeferredLighting");

        List<HRPLight> lights;

        Texture2D TransformInv_Diffuse, TransformInv_Specular, AmpDiffAmpSpecFresnel, DiscClip;

        Mesh sphere, quad, tube;

        public AreaLight()
        {
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere = go.GetComponent<MeshFilter>().sharedMesh;
                GameObject.DestroyImmediate(go);
            }
            {
                quad = new Mesh();
                quad.name = "Quad";
                quad.vertices = new Vector3[] { float3(-0.5f, -0.5f, 0), float3(0.5f, -0.5f, 0), float3(-0.5f, 0.5f, 0), float3(0.5f, 0.5f, 0) };
                quad.triangles = new int[] { 0, 1, 2, 2, 1, 3 };
                quad.uv = new Vector2[] { float2(1, 0), float2(0, 0), float2(1, 1), float2(0, 1) };
            }
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tube = go.GetComponent<MeshFilter>().sharedMesh;
                GameObject.DestroyImmediate(go);
            }
            lights = new List<HRPLight>();

            AmpDiffAmpSpecFresnel = Resources.Load<Texture2D>("Textures/LUT/AmpDiffAmpSpecFresnel");
            TransformInv_Diffuse = Resources.Load<Texture2D>("Textures/LUT/TransformInv_DisneyDiffuse");
            TransformInv_Specular = Resources.Load<Texture2D>("Textures/LUT/TransformInv_GGX");
            DiscClip = Resources.Load<Texture2D>("Textures/LUT/DiscClip");
        }


        public override void Excute(RenderContext context)
        {
            LightManager.GetVisibleLights(lights);

            context.CmdBuffer.SetGlobalTexture("_AmpDiffAmpSpecFresnel", AmpDiffAmpSpecFresnel);
            context.CmdBuffer.SetGlobalTexture("_TransformInv_Diffuse", TransformInv_Diffuse);
            context.CmdBuffer.SetGlobalTexture("_TransformInv_Specular", TransformInv_Specular);
            context.CmdBuffer.SetGlobalTexture("_DiscClip", DiscClip);

            context.CmdBuffer.SetGlobalTexture("_DepthTex", depth.handle);
            context.CmdBuffer.SetGlobalTexture("_BaseColorTex", baseColor_roughness.handle);
            context.CmdBuffer.SetGlobalTexture("_NormalTex", normal_metallic.handle);

            foreach (var light in lights)
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
                            context.CmdBuffer.SetRenderTarget(color: target.handle, depth: depth.handle);
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

                            context.CmdBuffer.Blit(null, target.handle, deferredLightingMat, 1);
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

                            context.CmdBuffer.Blit(null, target.handle, deferredLightingMat, 2);
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

                            context.CmdBuffer.Blit(null, target.handle, deferredLightingMat, 3);
                        }
                    }
                }
            }
        }
    }

}