using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.StationRadio.UI;

public sealed class PixelVolumeScale : Control
{
    /// <summary>
    /// Path to control's texture.
    /// </summary>
    private const string TexturePath = "/Textures/_Starlight/Interface/StationRadio/radio_scale.png";

    /// <summary>
    /// Const for determining pixel size.
    /// </summary>
    private const int PixelScale = 3;

    /// <summary>
    /// The value representing the left bound side. I.e. left sprite bound
    /// </summary>
    private const int Left = 4;

    /// <summary>
    /// The value representing the right bound side. I.e. right sprite bound
    /// </summary>
    private const int Right = 95;

    /// <summary>
    /// The value representing the top bound side. I.e. top sprite bound
    /// </summary>
    private const int Top = 2;

    /// <summary>
    /// The value representing the bottom bound side. I.e. bottom sprite bound
    /// </summary>
    private const int Bottom = 13;

    private static readonly Color _needle = Color.FromHex("#e3a148");
    private static readonly Color _needleOff = Color.FromHex("#5d6472");

    private readonly Texture _texture;

    public float Value;

    public bool Lit = true;

    public PixelVolumeScale()
    {
        _texture = IoCManager.Resolve<IResourceCache>().GetTexture(TexturePath);
        MouseFilter = MouseFilterMode.Ignore;
    }

    private float _scale => MathF.Max(1f, MathF.Round(PixelScale * UIScale));

    protected override Vector2 MeasureOverride(Vector2 availableSize) => _texture.Size * PixelScale;

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var scale = _scale;
        var size = (Vector2) _texture.Size * scale;
        var origin = ((PixelSize - size) / 2f).Floored();
        handle.DrawTextureRect(_texture, UIBox2.FromDimensions(origin, size));

        var x = MathF.Round(Left + (Math.Clamp(Value, 0f, 1f) * (Right - Left)));
        var needle = UIBox2.FromDimensions(
            origin + (new Vector2(x, Top) * scale),
            new Vector2(1, Bottom - Top + 1) * scale);
        handle.DrawRect(needle, Lit ? _needle : _needleOff);
    }
}
