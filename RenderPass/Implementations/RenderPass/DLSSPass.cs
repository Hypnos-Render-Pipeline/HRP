using HypnosRenderPipeline.Tools;
using UnityEditor;
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
        public float sharpness = 0.5f;

        public enum Quality
        {
            Low = (int)NVIDIA.DLSSQuality.UltraPerformance,
            Midle = (int)NVIDIA.DLSSQuality.MaximumPerformance,
            High = (int)NVIDIA.DLSSQuality.Balanced,
            Utra = (int)NVIDIA.DLSSQuality.MaximumQuality
        }

        public Quality quality = Quality.High;
        Quality quality_;

        private static uint s_ExpectedDeviceVersion = 0x04;

        NVIDIA.GraphicsDevice m_device;
        NVIDIA.DLSSContext m_dlssContext;

        Vector2Int inputSize = Vector2Int.zero;

        struct TextureIDs
        {
            public static int input = Shader.PropertyToID("dlss_input");
            public static int depth = Shader.PropertyToID("dlss_depth");
            public static int motion = Shader.PropertyToID("dlss_motion");
            public static int output = Shader.PropertyToID("dlss_output");
        }

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

            if (m_device == null)
            {
                Debug.LogError("Unkown Error about DLSS. Update your driver to latest and try again.");
                return;
            }
            
            if (!m_device.IsFeatureAvailable(NVIDIA.GraphicsDeviceFeature.DLSS))
            {
                Debug.LogError("Graphics card dont support DLSS");
                return;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            if (m_dlssContext != null)
            {
                CommandBuffer cb = new CommandBuffer();
                m_device.DestroyFeature(cb, m_dlssContext);
                Graphics.ExecuteCommandBuffer(cb);
            }
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

            if (m_dlssContext == null || input.desc.basicDesc.width != inputSize.x || input.desc.basicDesc.height != inputSize.y || quality != quality_)
            {
                if (m_dlssContext != null) {
                    m_device.DestroyFeature(cb, m_dlssContext);
                    m_dlssContext = null;
                }

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
                settings.quality = (NVIDIA.DLSSQuality)(int)quality;
                m_dlssContext = m_device.CreateFeature(cb, settings);

                m_dlssContext.executeData.mvScaleX = -input_desc.width;
                m_dlssContext.executeData.mvScaleY = -input_desc.height;
                m_dlssContext.executeData.subrectOffsetX = 0;
                m_dlssContext.executeData.subrectOffsetY = 0;
                m_dlssContext.executeData.subrectWidth = (uint)input_desc.width;
                m_dlssContext.executeData.subrectHeight = (uint)input_desc.height;
                m_dlssContext.executeData.preExposure = 1.0f;
                m_dlssContext.executeData.invertYAxis = 1u;
                m_dlssContext.executeData.invertXAxis = 0u;
                m_dlssContext.executeData.reset = 1;

                inputSize = new Vector2Int(input.desc.basicDesc.width, input.desc.basicDesc.height);
                quality_ = quality;
            }

            m_dlssContext.executeData.sharpness = sharpness;
            m_dlssContext.executeData.jitterOffsetX = -context.jitter.x;
            m_dlssContext.executeData.jitterOffsetY = -context.jitter.y;

            if (context.frameIndex == 0)
                m_dlssContext.executeData.reset = 1;

            RenderTexture input_ = context.resourcesPool.GetTexture(TextureIDs.input, input.desc.basicDesc);
            cb.CopyTexture(input, input_);
            RenderTexture depth_ = context.resourcesPool.GetTexture(TextureIDs.depth, depth.desc.basicDesc);
            cb.CopyTexture(depth, depth_);
            RenderTexture motion_ = context.resourcesPool.GetTexture(TextureIDs.motion, motion.desc.basicDesc);
            cb.CopyTexture(motion, motion_);
            var desc = output.desc.basicDesc;
            desc.enableRandomWrite = true;
            RenderTexture output_ = context.resourcesPool.GetTexture(TextureIDs.output, desc);

            var textureTable = new NVIDIA.DLSSTextureTable()
            {
                colorInput = input_,
                colorOutput = output_,
                depth = depth_,
                motionVectors = motion_
            };

            context.context.ExecuteCommandBuffer(cb);
            cb.Clear();

            m_device.ExecuteDLSS(cb, m_dlssContext, textureTable);

            cb.CopyTexture(output_, output);

            m_dlssContext.executeData.reset = 0;
        }
    }
}