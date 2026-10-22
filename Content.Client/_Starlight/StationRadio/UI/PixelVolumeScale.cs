using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.StationRadio.UI;

public sealed class PixelVolumeScale : Control
{
    private const string TexturePath = "/Textures/_Starlight/Interface/StationRadio/radio_scale.png";

    private const int PixelScale = 3;

    private const int Left = 4;
    private const int Right = 95;
    private const int Top = 2;
    private const int Bottom = 13;

    private static readonly Color Needle = Color.FromHex("#e3a148");
    private static readonly Color NeedleOff = Color.FromHex("#5d6472");

    private readonly Texture _texture;

    public float Value;

    public bool Lit = true;

    public PixelVolumeScale()
    {
        _texture = IoCManager.Resolve<IResourceCache>().GetTexture(TexturePath);
        MouseFilter = MouseFilterMode.Ignore;
    }

    private float Scale => MathF.Max(1f, MathF.Round(PixelScale * UIScale));

    protected override Vector2 MeasureOverride(Vector2 availableSize) => _texture.Size * PixelScale;

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var scale = Scale;
        var size = (Vector2) _texture.Size * scale;
        var origin = ((PixelSize - size) / 2f).Floored();
        handle.DrawTextureRect(_texture, UIBox2.FromDimensions(origin, size));

        var x = MathF.Round(Left + Math.Clamp(Value, 0f, 1f) * (Right - Left));
        var needle = UIBox2.FromDimensions(
            origin + new Vector2(x, Top) * scale,
            new Vector2(1, Bottom - Top + 1) * scale);
        handle.DrawRect(needle, Lit ? Needle : NeedleOff);
    }
}
