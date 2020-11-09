using System;
using UnityEditor.VFX;

namespace HypnosRenderPipeline.VFX.Editor
{
    class VFXHRPBinder : VFXSRPBinder
    {
        public override string templatePath { get { return HypnosRenderPipeline.PathDefine.path + "VFX/Runtime/Resources/Shaders"; } }
        public override string SRPAssetTypeStr { get { return "HypnosRenderPipelineAsset"; } } 
        public override Type SRPOutputDataType { get { return null; } } 
    }
}
