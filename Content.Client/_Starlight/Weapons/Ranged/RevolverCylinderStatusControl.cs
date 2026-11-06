using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Weapons.Ranged;

public sealed class RevolverCylinderStatusControl : Control
{
    private static readonly Color _bodyShadow = Color.FromHex("#15181b");
    private static readonly Color _bodyRim = Color.FromHex("#6f7883");
    private static readonly Color _bodyFace = Color.FromHex("#454c55");
    private static readonly Color _bodyInner = Color.FromHex("#39404a");
    private static readonly Color _axisRim = Color.FromHex("#5c646e");
    private static readonly Color _axis = Color.FromHex("#1a1d21");

    private static readonly Color _chamberRim = Color.FromHex("#1c2024");
    private static readonly Color _brass = Color.FromHex("#d9b248");
    private static readonly Color _brassShine = Color.FromHex("#f1d88a");
    private static readonly Color _primerRing = Color.FromHex("#9a7424");
    private static readonly Color _primer = Color.FromHex("#c8a04a");
    private static readonly Color _spent = Color.FromHex("#c4532d");
    private static readonly Color _spentHole = Color.FromHex("#2a120b");
    private static readonly Color _empty = Color.FromHex("#2b3137");
    private static readonly Color _emptyHole = Color.FromHex("#0e1113");
    private static readonly Color _topMarker = Color.FromHex("#5fd35f");

    // The time it takes to turn the cylinder one chamber after a shot.
    private const float ShotTurnTime = 0.25f;
    private const float SpinTime = 1.0f;
    private const int SpinExtraTurns = 2;

    // true - live round, false - spent, null - empty.
    private bool?[] _chambers = [];
    private int _index = -1;

    // The current chamber index being shown, which may be in between two chambers during a spin.
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

        // Calculate the number of chambers turned, wrapping around the cylinder if necessary.
        var step = (((currentIndex - _index) % capacity) + capacity) % capacity;
        _index = currentIndex;

        if (step == 0)
            return;

        // If the cylinder turned more than one chamber, spin it a couple of extra turns for visual flair.
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

    /// <summary>
    /// Eases out with a cubic curve, starting fast and slowing down towards the end.
    /// </summary>
    private static float EaseOutCubic(float t)
        => 1f - MathF.Pow(1f - t, 3f);

    /// <summary>
    /// Overshoots a bit, then settles back to the target value.
    /// </summary>
    private static float EaseOutBack(float t)
    {
        const float C1 = 1.2f;
        const float C3 = C1 + 1f;
        return 1f + (C3 * MathF.Pow(t - 1f, 3f)) + (C1 * MathF.Pow(t - 1f, 2f));
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
        // The minimum pixel size is 1, so that the rim and primer ring are always visible.
        var px = MathF.Max(1f, UIScale);

        // Cylinder body.
        handle.DrawCircle(center, outer, _bodyShadow);
        handle.DrawCircle(center, outer - px, _bodyRim);
        handle.DrawCircle(center, outer - (2f * px), _bodyFace);
        handle.DrawCircle(center, outer * 0.62f, _bodyInner);

        // Chamber radius and the ring on which they are placed. The ring is smaller for larger cylinders, to avoid overlapping chambers.
        var chamber = outer * (capacity <= 6 ? 0.29f : capacity <= 8 ? 0.24f : 0.18f);
        var ring = outer - chamber - (2f * px);

        for (var i = 0; i < capacity; i++)
        {
            // Angle in radians, with 0 at the top and increasing clockwise.
            var angle = ((i - _shown) / capacity * MathF.Tau) - (MathF.PI / 2f);
            var position = center + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ring);
            DrawChamber(handle, position, chamber, px, _chambers[i]);
        }

        // Center axis.
        handle.DrawCircle(center, outer * 0.2f, _axisRim);
        handle.DrawCircle(center, (outer * 0.2f) - px, _axis);

        DrawTopMarker(handle, center - new Vector2(0f, ring), chamber + (1.5f * px), px);
    }

    private static void DrawChamber(DrawingHandleScreen handle, Vector2 position, float radius, float px, bool? state)
    {
        handle.DrawCircle(position, radius, _chamberRim);
        var inner = radius - px;

        switch (state)
        {
            // Live round
            case true:
                handle.DrawCircle(position, inner, _brass);
                handle.DrawCircle(position, inner * 0.45f, _primerRing);
                if (inner >= 5f * px)
                {
                    handle.DrawCircle(position - new Vector2(inner * 0.35f, inner * 0.35f), inner * 0.25f, _brassShine);
                    handle.DrawCircle(position, inner * 0.28f, _primer);
                }
                break;
            // Spent casing
            case false:
                handle.DrawCircle(position, inner, _spent);
                handle.DrawCircle(position, inner * 0.5f, _spentHole);
                break;
            // Empty chamber
            default:
                handle.DrawCircle(position, inner, _empty);
                handle.DrawCircle(position, inner * 0.6f, _emptyHole);
                break;
        }
    }

    /// <summary>
    /// Draws a green arc over the top chamber, indicating the next chamber to fire.
    /// </summary>
    private static void DrawTopMarker(DrawingHandleScreen handle, Vector2 chamber, float radius, float px)
    {
        const int Segments = 10;
        const float From = -MathF.PI * 0.78f;
        const float To = -MathF.PI * 0.22f;

        for (var i = 0; i < Segments; i++)
        {
            var a = From + ((To - From) * i / (Segments - 1));
            handle.DrawCircle(chamber + (new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius), px, _topMarker);
        }
    }
}
