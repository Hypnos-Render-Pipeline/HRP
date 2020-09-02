namespace HypnosRenderPipeline.RenderPass
{
    public class LightListPin : BaseNodePin<int, LightList>
    {
        public override void CastFrom(RenderContext renderContext, BaseNodePin<int, LightList> pin)
        {
            handle.Copy(pin.handle);
        }

        public override void AllocateResourcces(RenderContext renderContext, int id)
        {
            base.AllocateResourcces(renderContext, id);
            handle.Clear();
        }
    }
}