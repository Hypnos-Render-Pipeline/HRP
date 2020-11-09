using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public class TerrainPass : BaseRenderPass
    {
        [NodePin(PinType.InOut)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24),
                                                        SizeCastMode.ResizeToInput,
                                                        ColorCastMode.Fixed,
                                                        SizeScale.Full);

        [NodePin(PinType.InOut)]
        public TexturePin baseColor_roughness = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                    SizeCastMode.Fixed,
                                                                    ColorCastMode.Fixed,
                                                                    SizeScale.Full);
        [NodePin(PinType.InOut)]
        public TexturePin normal_metallic = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                    SizeCastMode.Fixed,
                                                                    ColorCastMode.Fixed,
                                                                    SizeScale.Full);
        [NodePin(PinType.InOut)]
        public TexturePin microAO = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGB32, 0),
                                                                    SizeCastMode.Fixed,
                                                                    ColorCastMode.Fixed,
                                                                    SizeScale.Full);

        public bool gameCameraCull = true;

        HRPTerrain terrain = null;

        public override void Excute(RenderContext context)
        {
            var cb = context.commandBuffer;

            if (!baseColor_roughness.connected)
            {
                cb.SetRenderTarget(baseColor_roughness);
                cb.ClearRenderTarget(false, true, Color.clear);
            }
            if (!normal_metallic.connected)
            {
                cb.SetRenderTarget(normal_metallic);
                cb.ClearRenderTarget(false, true, Color.clear);
            }
            if (!microAO.connected)
            {
                cb.SetRenderTarget(microAO);
                cb.ClearRenderTarget(false, true, Color.clear);
            }

            cb.SetRenderTarget(new RenderTargetIdentifier[] { baseColor_roughness.handle, normal_metallic, microAO }, depth: depth);
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
                    context.context.ExecuteCommandBuffer(context.commandBuffer);
                    cb.Clear();
                    context.context.ExecuteCommandBuffer(terrain.cb);
                }
            }
            else
            {
                terrain = GameObject.FindObjectOfType<HRPTerrain>();
            }
        }
    }

}