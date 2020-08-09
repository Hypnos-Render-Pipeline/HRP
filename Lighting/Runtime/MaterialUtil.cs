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

        static MaterialWithName __depthBit__ = new MaterialWithName("Hidden/DepthBlit");
        
        public static Material depthBit { get { return __depthBit__; } }
    }


}