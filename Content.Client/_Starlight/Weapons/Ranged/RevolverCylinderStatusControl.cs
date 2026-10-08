using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Weapons.Ranged;

public sealed class RevolverCylinderStatusControl : Control
{
    private static readonly Color BodyShadow = Color.FromHex("#15181b");
    private static readonly Color BodyRim = Color.FromHex("#6f7883");
    private static readonly Color BodyFace = Color.FromHex("#454c55");
    private static readonly Color BodyInner = Color.FromHex("#39404a");
    private static readonly Color AxisRim = Color.FromHex("#5c646e");
    private static readonly Color Axis = Color.FromHex("#1a1d21");

    private static readonly Color ChamberRim = Color.FromHex("#1c2024");
    private static readonly Color Brass = Color.FromHex("#d9b248");
    private static readonly Color BrassShine = Color.FromHex("#f1d88a");
    private static readonly Color PrimerRing = Color.FromHex("#9a7424");
    private static readonly Color Primer = Color.FromHex("#c8a04a");
    private static readonly Color Spent = Color.FromHex("#c4532d");
    private static readonly Color SpentHole = Color.FromHex("#2a120b");
    private static readonly Color Empty = Color.FromHex("#2b3137");
    private static readonly Color EmptyHole = Color.FromHex("#0e1113");
    private static readonly Color TopMarker = Color.FromHex("#5fd35f");

    private const float ShotTurnTime = 0.25f;
    private const float SpinTime = 1.0f;
    private const int SpinExtraTurns = 2;

    private bool?[] _chambers = [];
    private int _index = -1;

    private float _shown;
    private float _from;
    private float _to;
    private float _elapsed;
    private float _duration;
    private bool _spinning;

    public RevolverCylinderStatusControl()
    {
        MinSize = new Vector2(32, 32);
        HorizontalAlignment = HAlignment.Left;
        VerticalAlignment = VAlignment.Bottom;
    }

    public void Update(int currentIndex, bool?[] chambers)
    {
        var capacity = chambers.Length;
        _chambers = chambers;

        if (capacity == 0 || _index < 0 || _index >= capacity)
        {
            _index = currentIndex;
            _shown = _from = _to = currentIndex;
            _duration = 0f;
            return;
        }

        var step = (((currentIndex - _index) % capacity) + capacity) % capacity;
        _index = currentIndex;

        if (step == 0)
            return;

        _spinning = step != 1;
        _from = _shown;
        _to += _spinning ? step + (SpinExtraTurns * capacity) : step;
        _elapsed = 0f;
        _duration = _spinning ? SpinTime : ShotTurnTime;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_duration <= 0f)
            return;

        _elapsed += args.DeltaSeconds;
        var t = Math.Clamp(_elapsed / _duration, 0f, 1f);
        _shown = _from + ((_to - _from) * (_spinning ? EaseOutCubic(t) : EaseOutBack(t)));

        if (t >= 1f)
        {
            _shown = _to;
            _duration = 0f;
        }
    }

    private static float EaseOutCubic(float t)
        => 1f - MathF.Pow(1f - t, 3f);

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.2f;
        const float c3 = c1 + 1f;
        return 1f + (c3 * MathF.Pow(t - 1f, 3f)) + (c1 * MathF.Pow(t - 1f, 2f));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var capacity = _chambers.Length;
        if (capacity == 0)
            return;

        var size = PixelSize;
        var outer = MathF.Min(size.X, size.Y) / 2f;
        var center = new Vector2(outer, size.Y / 2f);
        var px = MathF.Max(1f, UIScale);

        handle.DrawCircle(center, outer, BodyShadow);
        handle.DrawCircle(center, outer - px, BodyRim);
        handle.DrawCircle(center, outer - (2f * px), BodyFace);
        handle.DrawCircle(center, outer * 0.62f, BodyInner);

        var chamber = outer * (capacity <= 6 ? 0.29f : capacity <= 8 ? 0.24f : 0.18f);
        var ring = outer - chamber - (2f * px);

        for (var i = 0; i < capacity; i++)
        {
            var angle = ((i - _shown) / capacity * MathF.Tau) - (MathF.PI / 2f);
            var position = center + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ring);
            DrawChamber(handle, position, chamber, px, _chambers[i]);
        }

        handle.DrawCircle(center, outer * 0.2f, AxisRim);
        handle.DrawCircle(center, (outer * 0.2f) - px, Axis);

        DrawTopMarker(handle, center - new Vector2(0f, ring), chamber + (1.5f * px), px);
    }

    private static void DrawChamber(DrawingHandleScreen handle, Vector2 position, float radius, float px, bool? state)
    {
        handle.DrawCircle(position, radius, ChamberRim);
        var inner = radius - px;

        switch (state)
        {
            case true:
                handle.DrawCircle(position, inner, Brass);
                handle.DrawCircle(position, inner * 0.45f, PrimerRing);
                if (inner >= 5f * px)
                {
                    handle.DrawCircle(position - new Vector2(inner * 0.35f, inner * 0.35f), inner * 0.25f, BrassShine);
                    handle.DrawCircle(position, inner * 0.28f, Primer);
                }
                break;
            case false:
                handle.DrawCircle(position, inner, Spent);
                handle.DrawCircle(position, inner * 0.5f, SpentHole);
                break;
            default:
                handle.DrawCircle(position, inner, Empty);
                handle.DrawCircle(position, inner * 0.6f, EmptyHole);
                break;
        }
    }

    private static void DrawTopMarker(DrawingHandleScreen handle, Vector2 chamber, float radius, float px)
    {
        const int segments = 10;
        const float from = -MathF.PI * 0.78f;
        const float to = -MathF.PI * 0.22f;

        for (var i = 0; i < segments; i++)
        {
            var a = from + ((to - from) * i / (segments - 1));
            handle.DrawCircle(chamber + (new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius), px, TopMarker);
        }
    }
}
