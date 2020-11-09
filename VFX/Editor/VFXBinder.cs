using System;
using UnityEditor.VFX;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline.GraphWrapper.Editor
{
    class VFXHRPBinder : VFXSRPBinder
    {
        public override string templatePath { get { return PathDefine.path + "VFX/Runtime/Resources/Shaders"; } }
        public override string SRPAssetTypeStr { get { return "HypnosRenderPipelineAsset"; } } 
        public override Type SRPOutputDataType { get { return null; } } 
    }
}
