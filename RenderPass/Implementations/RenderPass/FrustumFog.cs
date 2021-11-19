using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;
using UnityEngine.Rendering;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline.RenderPass
{
    public class FrustumFog : BaseRenderPass
    {
        [NodePin(PinType.InOut, true)]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGBHalf));

        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);




        [NodePin(PinType.In)]
        public TexturePin skyIrradiance = new TexturePin(new RenderTextureDescriptor(32, 32, RenderTextureFormat.ARGBHalf) { dimension = TextureDimension.Cube }, sizeScale: SizeScale.Custom);

        [Range(8, 32)]
        public int screenGridSize = 16;

        [Range(32, 128)]
        public int depthSliceNum = 64;

        [Range(32, 256)]
        public float maxDepth = 128;


        int volume_density = Shader.PropertyToID("_FogVolume_density");
        int volume_scatter = Shader.PropertyToID("_FogVolume_scatter");
        int volume_phase = Shader.PropertyToID("_FogVolume_phase");
        int volume = Shader.PropertyToID("_FogVolume");
        int place_holder = Shader.PropertyToID("_PlaceHolder");

        ComputeShaderWithName write_volume = new ComputeShaderWithName("Shaders/Fog/WriteVolume");
        MaterialWithName marching_volume = new MaterialWithName("Hidden/MarchingVolume");

        public override void Execute(RenderContext context)
        {
            int3 volume_resolution = int3(context.camera.pixelWidth / screenGridSize, context.camera.pixelHeight / screenGridSize, depthSliceNum);

            var desc = new RenderTextureDescriptor(volume_resolution.x, volume_resolution.y, RenderTextureFormat.RInt, 0, 0);
            desc.dimension = TextureDimension.Tex3D;
            desc.volumeDepth = volume_resolution.z;
            desc.enableRandomWrite = true;
            desc.autoGenerateMips = false;
            desc.useMipMap = false;
            
            var cb = context.commandBuffer;

            cb.GetTemporaryRT(volume_density, desc);
            cb.GetTemporaryRT(volume_phase, desc);
            cb.GetTemporaryRT(volume_scatter, desc);
            cb.GetTemporaryRT(place_holder, volume_resolution.x, volume_resolution.y);

            int3 volume_dispatch_size = (volume_resolution + 3) / 4;
            cb.SetGlobalVector("_FogVolumeSize", float4(volume_resolution, maxDepth));
            cb.SetComputeTextureParam(write_volume, 0, "_Volume0", volume_density);
            cb.SetComputeTextureParam(write_volume, 0, "_Volume1", volume_scatter);
            cb.SetComputeTextureParam(write_volume, 0, "_Volume2", volume_phase);
            cb.DispatchCompute(write_volume, 0, volume_dispatch_size.x, volume_dispatch_size.y, volume_dispatch_size.z);

            cb.SetRenderTarget(place_holder, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
            //cb.ClearRenderTarget(false, true, Color.clear);
            cb.SetRandomWriteTarget(1, volume_density);
            cb.SetRandomWriteTarget(2, volume_scatter);
            cb.SetRandomWriteTarget(3, volume_phase);
            cb.SetGlobalTexture("_Volume0", volume_density);
            cb.SetGlobalTexture("_Volume1", volume_scatter);
            cb.SetGlobalTexture("_Volume2", volume_phase);

            var a = new DrawingSettings(new ShaderTagId("Fog"), new SortingSettings(context.camera) { criteria = SortingCriteria.None });
            a.enableInstancing = false;
            a.enableDynamicBatching = false;

            var b = FilteringSettings.defaultValue;
            b.renderQueueRange = new RenderQueueRange((int)RenderQueue.Transparent + 1, (int)RenderQueue.Transparent + 1);

            cb.DrawRenderers(context.defaultCullingResult, ref a, ref b);
            cb.ClearRandomWriteTargets();

            cb.SetComputeTextureParam(write_volume, 1, "_Volume0", volume_density);
            cb.SetComputeTextureParam(write_volume, 1, "_Volume1", volume_scatter);
            cb.SetComputeTextureParam(write_volume, 1, "_Volume2", volume_phase);
            desc.colorFormat = RenderTextureFormat.ARGBHalf;
            cb.GetTemporaryRT(volume, desc);
            cb.SetComputeTextureParam(write_volume, 1, "_Volume", volume);
            cb.DispatchCompute(write_volume, 1, volume_dispatch_size.x, volume_dispatch_size.y, volume_dispatch_size.z);
            cb.ReleaseTemporaryRT(volume_density);
            cb.ReleaseTemporaryRT(volume_phase);
            cb.ReleaseTemporaryRT(volume_scatter);

            //cb.Blit(place_holder, target);
            cb.ReleaseTemporaryRT(place_holder);

            cb.Blit(volume, target, marching_volume, 0);
        }
    }
}