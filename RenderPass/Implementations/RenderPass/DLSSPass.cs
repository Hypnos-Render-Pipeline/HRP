using HypnosRenderPipeline.Tools;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using NVIDIA = UnityEngine.NVIDIA;

namespace HypnosRenderPipeline.RenderPass
{    
    [RenderGraph.RenderNodeInformation("DLSS support must be enabled on the Pipeline Asset to make this node work correctly.")]
    public class DLSSPass : BaseRenderPass
    {
        [NodePin(PinType.In, true)]
        public TexturePin input = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.DefaultHDR));

        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        [NodePin(PinType.In, true)]
        public TexturePin motion = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.RGFloat, 0), colorCastMode: ColorCastMode.Fixed);

        [NodePin(PinType.Out)]
        public TexturePin output;

        [Range(0, 1)]
        public float sharpness;

        private static uint s_ExpectedDeviceVersion = 0x04;

        NVIDIA.GraphicsDevice m_device;
        NVIDIA.DLSSContext m_dlssContext;

        public DLSSPass()
        {
            output = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.DefaultHDR), srcPin: input, sizeScale: SizeScale.Double);

            if (!NVIDIA.NVUnityPlugin.IsLoaded())
                NVIDIA.NVUnityPlugin.Load();

            if (!NVIDIA.NVUnityPlugin.IsLoaded())
            {
                Debug.LogError("Load DLSS failed.");
                return;
            }

            if (s_ExpectedDeviceVersion != NVIDIA.GraphicsDevice.version)
            {
                Debug.LogError("Cannot instantiate NVIDIA device because the version expects does not match the backend version.");
                return;
            }

            if (!SystemInfo.graphicsDeviceVendor.ToLower().Contains("nvidia"))
            {
                Debug.LogError("Only NV cards support DLSS");
                return;
            }
            
            if (m_device == null)
            {
                m_device = NVIDIA.GraphicsDevice.CreateGraphicsDevice("HRP");
            }

            if (m_device == null || !m_device.IsFeatureAvailable(NVIDIA.GraphicsDeviceFeature.DLSS))
            {
                Debug.LogError("Unkown Error about DLSS");
                return;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        public override void DisExecute(RenderContext context)
        {
            context.commandBuffer.Blit(input, output);
        }

        public override void Execute(RenderContext context)
        {
            var cb = context.commandBuffer;

            if (m_device == null || !context.enableDLSS)
            {
                cb.Blit(input, output);
                return;
            }

            //context.commandBuffer.Blit(input, output, mat, 0);

            var settings = new NVIDIA.DLSSCommandInitializationData();
            settings.SetFlag(NVIDIA.DLSSFeatureFlags.IsHDR, true);
            settings.SetFlag(NVIDIA.DLSSFeatureFlags.MVLowRes, true);
            settings.SetFlag(NVIDIA.DLSSFeatureFlags.DepthInverted, true);
            settings.SetFlag(NVIDIA.DLSSFeatureFlags.DoSharpening, true);
            var input_desc = input.desc.basicDesc;
            var output_desc = output.desc.basicDesc;
            settings.inputRTWidth = (uint)input_desc.width;
            settings.inputRTHeight = (uint)input_desc.height;
            settings.outputRTWidth = (uint)output_desc.width;
            settings.outputRTHeight = (uint)output_desc.height;
            settings.quality = NVIDIA.DLSSQuality.MaximumQuality;
            m_dlssContext = m_device.CreateFeature(cb, settings);


            m_dlssContext.executeData.sharpness = sharpness;
            m_dlssContext.executeData.mvScaleX = (float)input_desc.width / output_desc.width;
            m_dlssContext.executeData.mvScaleY = (float)input_desc.height / output_desc.height;
            m_dlssContext.executeData.subrectOffsetX = 0;
            m_dlssContext.executeData.subrectOffsetY = 0;
            m_dlssContext.executeData.subrectWidth = (uint)input_desc.width;
            m_dlssContext.executeData.subrectHeight = (uint)input_desc.height;
            m_dlssContext.executeData.jitterOffsetX = context.jitter.x;
            m_dlssContext.executeData.jitterOffsetY = context.jitter.y;
            m_dlssContext.executeData.preExposure = 1.0f;
            m_dlssContext.executeData.invertYAxis = 1u;
            m_dlssContext.executeData.invertXAxis = 0u;
            m_dlssContext.executeData.reset = context.frameIndex == 0 ? 1 : 0;


            RenderTexture input_ = RenderTexture.GetTemporary(input.desc.basicDesc);
            cb.CopyTexture(input, input_);
            RenderTexture depth_ = RenderTexture.GetTemporary(depth.desc.basicDesc);
            cb.CopyTexture(depth, depth_);
            RenderTexture motion_ = RenderTexture.GetTemporary(motion.desc.basicDesc);
            cb.CopyTexture(motion, motion_);
            RenderTexture output_ = RenderTexture.GetTemporary(output.desc.basicDesc);

            var textureTable = new NVIDIA.DLSSTextureTable()
            {
                colorInput = input_,
                colorOutput = output_,
                depth = depth_,
                motionVectors = motion_
            };

            m_device.ExecuteDLSS(cb, m_dlssContext, textureTable);

            cb.CopyTexture(output_, output);

            RenderTexture.ReleaseTemporary(input_);
            RenderTexture.ReleaseTemporary(depth_);
            RenderTexture.ReleaseTemporary(motion_);
            RenderTexture.ReleaseTemporary(output_);
        }
    }
}