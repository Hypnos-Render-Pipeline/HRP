using System.Collections.Generic;
using System.Diagnostics;

namespace HypnosRenderPipeline.RenderPass
{
    public class ZBin : BaseRenderPass
    {
        [NodePin(PinType.In, true)]
        public LightListPin lights = new LightListPin();

        [NodePin(PinType.Out)]
        public BufferPin<LightStructGPU> lightBuffer = new BufferPin<LightStructGPU>(200);

        List<LightStructGPU> lightBufferCPU = new List<LightStructGPU>();

        static ComputeShaderWithName zbin = new ComputeShaderWithName("Shaders/Tools/ZBin");

        public override void Excute(RenderContext context)
        {
            var local_lights = lights.handle.locals;
            var cam = context.RenderCamera;
            var cb = context.CmdBuffer;
            var lightCount = local_lights.Count;

            lightBuffer.ReSize(lightCount);

            cb.SetGlobalInt("_LocalLightCount", lightCount);
            cb.SetGlobalBuffer("_LocalLightBuffer", lightBuffer);

            if (lightCount == 0) return;

            lightBufferCPU.Clear();
            foreach (var light in local_lights)
            {
                lightBufferCPU.Add(light.lightStructGPU);
            }
            lightBuffer.SetData(lightBufferCPU);

            //cb.DispatchCompute(zbin, 0, lightCount / 32 + (lightCount % 32 != 0 ? 1 : 0), 1, 1);
        }
    }
}