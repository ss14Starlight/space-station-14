using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.StationRadio.UI;

public sealed class PixelDialProgress : Control
{
    private const string TexturePath = "/Textures/_Starlight/Interface/StationRadio/radio_scale.png";

    private const int PixelScale = 2;

    private const int Left = 2;
    private const int Right = 97;
    private const int Top = 2;
    private const int Bottom = 13;

    private static readonly Color _fill = Color.FromHex("#4fc25780");
    private static readonly Color _fillPaused = Color.FromHex("#e3c44780");

    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly Texture _texture;

    private bool _playing;
    private TimeSpan _position;
    private TimeSpan _playbackStart;

    public TimeSpan Elapsed
    {
        get
        {
            var position = _playing ? _timing.CurTime - _playbackStart : _position;
            if (position > Length)
                position = Length;
            return position < TimeSpan.Zero ? TimeSpan.Zero : position;
        }
    }

    public TimeSpan Length { get; private set; }

    public PixelDialProgress()
    {
        _texture = IoCManager.Resolve<IResourceCache>().GetTexture(TexturePath);
        MouseFilter = MouseFilterMode.Ignore;
    }

    public void SetPlayback(bool playing, TimeSpan length, TimeSpan position, TimeSpan playbackStart)
    {
        _playing = playing;
        Length = length;
        _position = position;
        _playbackStart = playbackStart;
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

        if (Length <= TimeSpan.Zero)
            return;

        var fraction = (float) (Elapsed / Length);
        var width = MathF.Round(fraction * (Right - Left + 1));
        if (width <= 0)
            return;

        var fill = UIBox2.FromDimensions(
            origin + (new Vector2(Left, Top) * scale),
            new Vector2(width, Bottom - Top + 1) * scale);
        handle.DrawRect(fill, _playing ? _fill : _fillPaused);
    }
}
