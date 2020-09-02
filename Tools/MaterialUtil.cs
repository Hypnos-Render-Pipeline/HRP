using UnityEngine;

namespace HypnosRenderPipeline
{
    public class MaterialWithName
    {
        string shadername;
        Material mat;
        public MaterialWithName(string shader)
        {
            shadername = shader;
        }
        public Material material { get { if (mat == null) mat = new Material(Shader.Find(shadername)); return mat; } }

        public static implicit operator Material(MaterialWithName a)
        {
            return a.material;
        }

        public static MaterialWithName depthBlit = new MaterialWithName("Hidden/DepthBlit");

        public static MaterialWithName debugBlit = new MaterialWithName("Hidden/DebugBlit");
    }

    public class ComputeShaderWithName
    {
        string path;
        ComputeShader __Shader__;
        ComputeShader Shader { get { if (__Shader__ == null) __Shader__ = Resources.Load<ComputeShader>(path); return __Shader__; } }

        public ComputeShaderWithName(string path)
        {
            this.path = path;
        }

        public static implicit operator ComputeShader(ComputeShaderWithName a)
        {
            return a.Shader;
        }
        public static ComputeShaderWithName cullingShader = new ComputeShaderWithName("Shaders/Tools/FrustumCulling");
    }

}