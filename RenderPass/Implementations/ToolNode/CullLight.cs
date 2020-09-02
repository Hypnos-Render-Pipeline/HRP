using UnityEngine;

namespace HypnosRenderPipeline.RenderPass
{
    public class CullLight : BaseToolNode
    {
        [NodePin(PinType.Out)]
        public LightListPin lights = new LightListPin();

        public enum CullingType { Frustum, Sphere };
        public CullingType cullingType = CullingType.Frustum;

        [Tooltip("When using Sphere culling, radius is culling radius")]
        public float radius = 200;
        [Tooltip("how much distance should light become faraway light")]
        public float faraway = 100;


        public override void Excute(RenderContext context)
        {
            if (cullingType == CullingType.Frustum)
            {
                LightManager.GetVisibleLights(lights, context.RenderCamera, faraway);
            }
            else
            {
                LightManager.GetVisibleLights(lights, context.RenderCamera, radius, faraway);
            }
        }
    }
}