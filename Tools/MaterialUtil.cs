using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace HypnosRenderPipeline.Tools
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
        public static MaterialWithName clearBlit = new MaterialWithName("Hidden/ClearAlpha");
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

    public class RTShaderWithName
    {
        string path;
        RayTracingShader __Shader__;
        RayTracingShader Shader { get { if (__Shader__ == null) __Shader__ = Resources.Load<RayTracingShader>(path); return __Shader__; } }

        public RTShaderWithName(string path)
        {
            this.path = path;
        }

        public static implicit operator RayTracingShader(RTShaderWithName a)
        {
            return a.Shader;
        }
    }
}