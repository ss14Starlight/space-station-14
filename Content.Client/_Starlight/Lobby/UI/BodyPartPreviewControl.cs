using System.Numerics;
using Content.Client.Clickable;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Lobby.UI;

/// <summary>
/// One sprite layer of the body preview. Only its opaque pixels can be clicked, so overlapping parts pick right.
/// </summary>
public sealed class BodyPartPreviewControl : Control
{
    private static readonly Color DimColor = new(0.3f, 0.3f, 0.3f, 0.5f);
    private static readonly Dictionary<(RSI, RSI.StateId, RsiDirection), UIBox2?> BoundsCache = new();

    private readonly Texture[] _frames;
    private readonly float[] _delays;
    private int _frame;
    private float _frameTime;
    private readonly Color _color;
    private Vector2 _scale;
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
        if (rsi.TryGetState(state, out var rsiState) && rsiState.IsAnimated)
        {
            _frames = rsiState.GetFrames(direction);
            _delays = rsiState.GetDelays();
        }
        else
        {
            _frames = new[] { texture };
            _delays = new[] { 0f };
        }
        _color = color;
        _scale = scale;
        _clickMap = clickMap;

        SetScale(scale);
        MouseFilter = part != null ? MouseFilterMode.Stop : MouseFilterMode.Ignore;
    }

    /// <summary>
    /// Size of the layer's frame in sprite pixels.
    /// </summary>
    public Vector2 FrameSize => _frames[0].Size;

    /// <summary>
    /// Sets how many screen pixels one sprite pixel takes, per axis.
    /// </summary>
    public void SetScale(Vector2 scale)
    {
        _scale = scale;
        SetSize = FrameSize * scale;
    }

    /// <summary>
    /// The box around the opaque pixels of the first frame, in sprite pixels from the frame's top left. Null for a blank frame.
    /// </summary>
    public UIBox2? OpaqueBounds()
    {
        var key = (_rsi, _state, _direction);
        if (!BoundsCache.TryGetValue(key, out var bounds))
            BoundsCache[key] = bounds = ScanBounds();
        return bounds;
    }

    private UIBox2? ScanBounds()
    {
        var size = (Vector2i) FrameSize;
        var min = new Vector2i(int.MaxValue, int.MaxValue);
        var max = new Vector2i(int.MinValue, int.MinValue);
        for (var y = 0; y < size.Y; y++)
        {
            for (var x = 0; x < size.X; x++)
            {
                if (!_clickMap.IsOccluding(_rsi, _state, _direction, 0, new Vector2i(x, y)))
                    continue;
                min = Vector2i.ComponentMin(min, new Vector2i(x, y));
                max = Vector2i.ComponentMax(max, new Vector2i(x + 1, y + 1));
            }
        }

        return min.X == int.MaxValue ? null : new UIBox2(min, max);
    }

    protected override Vector2 MeasureCore(Vector2 availableSize) => FrameSize * _scale;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_frames.Length < 2)
            return;

        _frameTime += args.DeltaSeconds;
        while (_delays[_frame] > 0 && _frameTime >= _delays[_frame])
        {
            _frameTime -= _delays[_frame];
            _frame = (_frame + 1) % _frames.Length;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var color = Dimmed ? _color * DimColor : _color;
        if (_displacement != null)
            handle.UseShader(_displacement);
        handle.DrawTextureRect(_frames[_frame], UIBox2.FromDimensions(Vector2.Zero, PixelSize), color);
        if (_displacement != null)
            handle.UseShader(null);
    }

    protected override bool HasPoint(Vector2 point)
    {
        if (Part == null || point.X < 0 || point.Y < 0 || point.X >= Size.X || point.Y >= Size.Y)
            return false;

        return _clickMap.IsOccluding(_rsi, _state, _direction, _frame, (Vector2i) (point / _scale));
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (Part != null && args.Function == Robust.Shared.Input.EngineKeyFunctions.UIClick)
            Pressed?.Invoke(Part);
    }
}
