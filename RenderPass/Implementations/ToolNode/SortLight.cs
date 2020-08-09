using System.Collections.Generic;
using UnityEngine;

namespace HypnosRenderPipeline.RenderPass
{
    public class LightListPin : BaseNodePin<int, List<HRPLight>>
    {
        public override void CastFrom(RenderContext renderContext, BaseNodePin<int, List<HRPLight>> pin)
        {
            handle.Clear();
            handle.Capacity = pin.handle.Count;
            handle.AddRange(pin.handle);
        }
    }

    public class SortLight : BaseToolNode
    {
        [NodePin(PinType.Out)]
        [Tooltip("Main lights, they are closest to camera and have high quality shadow.")]
        public LightListPin mainLights = new LightListPin();

        [NodePin(PinType.Out)]
        [Tooltip("Second level lights, they are midle far from camera and still have shadow on.")]
        public LightListPin secondLights = new LightListPin();

        [NodePin(PinType.Out)]
        [Tooltip("Other lights, they are far from camera and don't need shadow.")]
        public LightListPin otherLights = new LightListPin();

        public override void Excute(RenderContext context)
        {
            LightManager.GetVisibleLights(mainLights.handle);
        }
    }
}