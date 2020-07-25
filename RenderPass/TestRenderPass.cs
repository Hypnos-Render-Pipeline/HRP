using UnityEngine;

namespace HypnosRenderPipeline.RenderPass
{

    public class EighthScale : BaseRenderPass
    {
        [NodePin]
        [Tooltip("It's a test inout pin.")]
        public TexturePin EighthResInOut = new TexturePin(new TexturePin.TexturePinDesc(
                                                            new RenderTextureDescriptor(1,1),
                                                            TexturePin.TexturePinDesc.SizeCastMode.Fixed,
                                                            TexturePin.TexturePinDesc.ColorCastMode.FitToInput,
                                                            TexturePin.TexturePinDesc.SizeScale.Eighth));

        public override void Excute(RenderContext context)
        {
        }
    }

    public class ClearWithColor : BaseRenderPass
    {
        [NodePin]
        [Tooltip("It's a test inout pin.")]
        public TexturePin target = new TexturePin(new TexturePin.TexturePinDesc(
                                                            new RenderTextureDescriptor(1, 1),
                                                            TexturePin.TexturePinDesc.SizeCastMode.Fixed,
                                                            TexturePin.TexturePinDesc.ColorCastMode.FitToInput,
                                                            TexturePin.TexturePinDesc.SizeScale.Eighth));

        [Tooltip("AA")]
        [ColorUsage(true, true)]
        public Color k;

        public override void Excute(RenderContext context)
        {
            context.CmdBuffer.SetRenderTarget(target.handle);
            context.CmdBuffer.ClearRenderTarget(true, true, k);
        }
    }

    public class LoadTexture : BaseToolNode
    {
        [NodePin(type: PinType.Out)]
        public TexturePin FullResOutput = new TexturePin(new TexturePin.TexturePinDesc(new RenderTextureDescriptor(1,1)));

        [Tooltip("Texture to show")]
        public Texture2D tex;

        public override void Excute(RenderContext context)
        {
            if (tex != null)
            {
                context.CmdBuffer.Blit(tex, FullResOutput.handle);
            }
        }
    }

    public class TestParameters : BaseRenderPass
    {
        [NodePin]
        [Tooltip("It's a test inout pin.")]
        public TexturePin pin = new TexturePin(new TexturePin.TexturePinDesc(
                                                            new RenderTextureDescriptor(1, 1),
                                                            TexturePin.TexturePinDesc.SizeCastMode.ResizeToInput,
                                                            TexturePin.TexturePinDesc.ColorCastMode.FitToInput,
                                                            TexturePin.TexturePinDesc.SizeScale.Full));

        public bool Boolen;

        [Tooltip("A")]
        public Color color;

        [Tooltip("B")]
        [ColorUsage(true, true)]
        public Color hdrColor;

        public int i;

        [Range(0, 10)]
        public int slideri;

        public float f;

        [Range(0, 1.0f)]
        public float sliderf;

        public Vector2 v2;

        public Vector2Int v2i;

        public Vector3 v3;

        public Vector3Int v3i;

        public Vector4 v4;

        public enum Enum
        {
            A,B,C,D
        }

        public Enum @enum;

        public Texture texture;

        public Camera camera;

        public BoxCollider collider;

        public Transform trans;

        public GameObject go;


        public override void Excute(RenderContext context)
        {
        }
    }
}