using System.Numerics;
using Content.Client.Clickable;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics.RSI;

namespace Content.Client._Starlight.Lobby.UI;

/// <summary>
/// One sprite layer of the body preview. Only its opaque pixels can be clicked, so overlapping parts pick right.
/// </summary>
public sealed class BodyPartPreviewControl : Control
{
    private static readonly Color DimColor = new(0.3f, 0.3f, 0.3f, 0.5f);

    private readonly Texture _texture;
    private readonly Color _color;
    private readonly Vector2 _scale;
    private readonly IClickMapManager _clickMap;
    private readonly RSI _rsi;
    private readonly RSI.StateId _state;
    private readonly RsiDirection _direction;

    /// <summary>
    /// The displacement shader of the layer, set up with its map, or null when the layer isn't displaced.
    /// </summary>
    private readonly ShaderInstance? _displacement;

    public BodyEditorPart? Part { get; }

    public bool Dimmed { get; set; }

    public event Action<BodyEditorPart>? Pressed;

    public BodyPartPreviewControl(BodyEditorPart? part, RSI rsi, RSI.StateId state, RsiDirection direction,
        Texture texture, Color color, Vector2 scale, IClickMapManager clickMap, ShaderInstance? displacement = null)
    {
        _displacement = displacement;
        Part = part;
        _rsi = rsi;
        _state = state;
        _direction = direction;
        _texture = texture;
        _color = color;
        _scale = scale;
        _clickMap = clickMap;

        SetSize = new Vector2(texture.Width, texture.Height) * scale;
        MouseFilter = part != null ? MouseFilterMode.Stop : MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var color = Dimmed ? _color * DimColor : _color;
        if (_displacement != null)
            handle.UseShader(_displacement);
        handle.DrawTextureRect(_texture, UIBox2.FromDimensions(Vector2.Zero, PixelSize), color);
        if (_displacement != null)
            handle.UseShader(null);
    }

    protected override bool HasPoint(Vector2 point)
    {
        if (Part == null || point.X < 0 || point.Y < 0 || point.X >= Size.X || point.Y >= Size.Y)
            return false;

        return _clickMap.IsOccluding(_rsi, _state, _direction, 0, (Vector2i) (point / _scale));
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (Part != null && args.Function == Robust.Shared.Input.EngineKeyFunctions.UIClick)
            Pressed?.Invoke(Part);
    }
}
