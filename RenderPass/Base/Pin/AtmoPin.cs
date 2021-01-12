namespace HypnosRenderPipeline.RenderPass
{
    public class AfterAtmo : BaseNodePin<int, int>
    {

        public HRPAtmo atmo;

        public override void Move(BaseNodePin<int, int> pin)
        {
            base.Move(pin);
            atmo = (pin as AfterAtmo).atmo;
        }
    }
}
