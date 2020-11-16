using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline.RenderPass
{

    public class PyramidColor : BaseRenderPass
    {
        public static class PyramidColorUniform
        {
            public static int PrevLevelColor = Shader.PropertyToID("_Source");
            public static int CurrLevelColor = Shader.PropertyToID("_Result");
            public static int PrevCurr_Size = Shader.PropertyToID("_Size");
            public static int ColorPyramidNumLOD = Shader.PropertyToID("ColorPyramidNumLOD");
        }

        [NodePin(PinType.In, true)]
        public TexturePin filterTarget = new TexturePin(new RenderTextureDescriptor(1, 1));

        [NodePin(PinType.Out)]
        public TexturePin pyramidColor;


        private int[] ColorPyramidMipIDs;
        static ComputeShaderWithName PyramidColorShader = new ComputeShaderWithName("Shaders/Tools/PyramidColorGenerator");


        public PyramidColor()
        {
            ColorPyramidInit();
            pyramidColor = new TexturePin(new RenderTextureDescriptor(1, 1) { bindMS = false, useMipMap = true, autoGenerateMips = false, dimension = TextureDimension.Tex2D }, srcPin: filterTarget);

        }

        public void ColorPyramidInit()
        {
            if (ColorPyramidMipIDs == null || ColorPyramidMipIDs.Length == 0) {
                ColorPyramidMipIDs = new int[12];

                for (int i = 0; i < 12; i++) {
                    ColorPyramidMipIDs[i] = Shader.PropertyToID("_SSSRGaussianMip" + i);
                }
            }
        }

        public void ColorPyramidUpdate(int2 ScreenSize, RenderTargetIdentifier DstRT , CommandBuffer CmdBuffer)
        {
            int ColorPyramidCount = Mathf.FloorToInt(Mathf.Log(ScreenSize.x, 2) - 3);
            ColorPyramidCount = Mathf.Min(ColorPyramidCount, 12);
            CmdBuffer.SetGlobalFloat(PyramidColorUniform.ColorPyramidNumLOD, (float)ColorPyramidCount);
            RenderTargetIdentifier PrevColorPyramid = DstRT;
            int2 ColorPyramidSize = ScreenSize;
            for (int i = 0; i < ColorPyramidCount; i++) {
                ColorPyramidSize.x >>= 1;
                ColorPyramidSize.y >>= 1;

                CmdBuffer.GetTemporaryRT(ColorPyramidMipIDs[i], ColorPyramidSize.x, ColorPyramidSize.y, 0, FilterMode.Bilinear, filterTarget.desc.basicDesc.colorFormat, RenderTextureReadWrite.Default, 1, true);
                CmdBuffer.SetComputeTextureParam(PyramidColorShader, 0, PyramidColorUniform.PrevLevelColor, PrevColorPyramid);
                CmdBuffer.SetComputeTextureParam(PyramidColorShader, 0, PyramidColorUniform.CurrLevelColor, ColorPyramidMipIDs[i]);
                CmdBuffer.SetComputeVectorParam(PyramidColorShader, PyramidColorUniform.PrevCurr_Size, new float4(ColorPyramidSize.x, ColorPyramidSize.y, 1f / ColorPyramidSize.x, 1f / ColorPyramidSize.y));
                CmdBuffer.DispatchCompute(PyramidColorShader, 0, Mathf.CeilToInt(ColorPyramidSize.x / 8f), Mathf.CeilToInt(ColorPyramidSize.y / 8f), 1);
                CmdBuffer.CopyTexture(ColorPyramidMipIDs[i], 0, 0, DstRT, 0, i + 1);

                PrevColorPyramid = ColorPyramidMipIDs[i];
            } for (int i = 0; i < ColorPyramidCount; i++) {
                CmdBuffer.ReleaseTemporaryRT(ColorPyramidMipIDs[i]);
            }
        }

        public override void Excute(RenderContext context)
        {
            context.context.ExecuteCommandBuffer(context.commandBuffer);
            context.commandBuffer.Clear();

            int2 ScreenSize = new int2(context.camera.pixelWidth, context.camera.pixelHeight);
            context.commandBuffer.Blit(filterTarget, pyramidColor);

            ColorPyramidUpdate(ScreenSize, pyramidColor, context.commandBuffer);
        }
    }
}