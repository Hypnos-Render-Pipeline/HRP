using UnityEngine;

namespace HypnosRenderPipeline.Tools
{

    public class MeshWithType
    {
        PrimitiveType type;
        Mesh mesh_;
        public MeshWithType(PrimitiveType type)
        {
            this.type = type;
        }
        public Mesh mesh { get { 
                if (mesh_ == null) {
                    var go = GameObject.CreatePrimitive(type);
                    mesh_ = go.GetComponent<MeshFilter>().sharedMesh;
                    GameObject.DestroyImmediate(go);
                }
                return mesh_;
            }}

        public static implicit operator Mesh(MeshWithType a)
        {
            return a.mesh;
        }

        static MeshWithType __quad__ = new MeshWithType(PrimitiveType.Quad);
        public static Mesh quad { get { return __quad__; } }


        static MeshWithType __sphere__ = new MeshWithType(PrimitiveType.Sphere);
        public static Mesh sphere { get { return __sphere__; } }


        static MeshWithType __cube__ = new MeshWithType(PrimitiveType.Cube);
        public static Mesh cube { get { return __cube__; } }


        static MeshWithType __cylinder__ = new MeshWithType(PrimitiveType.Cylinder);
        public static Mesh cylinder { get { return __cylinder__; } }
    }
}