using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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

        static MaterialWithName __depthBlit__ = new MaterialWithName("Hidden/DepthBlit");
        public static Material depthBlit { get { return __depthBlit__; } }

        static MaterialWithName __debugBlit__ = new MaterialWithName("Hidden/DebugBlit");
        public static Material debugBlit { get { return __debugBlit__; } }
    }


}