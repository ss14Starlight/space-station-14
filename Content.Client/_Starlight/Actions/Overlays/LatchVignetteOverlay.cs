using System.Numerics;
using System.Runtime.InteropServices;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Actions.Overlays;

/// <summary>
/// Jagged teeth vignette shown to the latch target - red at the tips,
/// fading to black at their base. Fixed length; on each Bite Harder hit the
/// whole row pushes toward screen center, backed by a solid black fill,
/// then eases back to baseline.
/// </summary>
public sealed partial class LatchVignetteOverlay : Robust.Client.Graphics.Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private const int ToothCount = 14;
    private const float ToothLengthFraction = 0.10f;
    private const float BaselinePushFraction = 0.10f;
    private const float BitePushFraction = 0.32f;
    private const float DecayPerSecond = 2.5f;

    private static readonly Color _tipColor = Color.FromSrgb(new Color(0.7f, 0f, 0f, 0.9f));
    private static readonly Color _baseColor = Color.FromSrgb(new Color(0f, 0f, 0f, 0.9f));
    private static readonly Color _fillColor = new(0f, 0f, 0f, 0.9f);

    // Deterministic per-tooth length variance so the row reads as jagged
    // rather than a perfectly even sawtooth.
    private static readonly float[] _toothJitter =
    {
        0f, 0.4f, -0.25f, 0.15f, -0.4f, 0.3f, 0f,
        -0.3f, 0.4f, -0.15f, 0.25f, -0.35f, 0.1f, -0.1f,
    };

    /// <summary>
    /// 0 at rest, 1 at a bite's peak. LatchSystem sets this to 1 on each
    /// Bite Harder hit; it eases back down here every frame.
    /// </summary>
    public float BiteIntensity;

    private readonly List<DrawVertexUV2DColor> _toothVerts = [];
    private readonly List<Vector2> _fillVerts = [];

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        BiteIntensity = MathF.Max(0f, BiteIntensity - DecayPerSecond * args.DeltaSeconds);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var bounds = args.ViewportBounds;
        var width = (float)bounds.Width;
        var height = (float)bounds.Height;

        var pushDepth = height * (BaselinePushFraction + (BitePushFraction - BaselinePushFraction) * BiteIntensity);
        var toothLength = height * ToothLengthFraction;

        _fillVerts.Clear();
        BuildFill(width, pushDepth, fromTop: true, screenHeight: height);
        BuildFill(width, pushDepth, fromTop: false, screenHeight: height);
        args.ScreenHandle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _fillVerts, _fillColor);

        _toothVerts.Clear();
        BuildToothRow(width, pushDepth, toothLength, fromTop: true, screenHeight: height);
        BuildToothRow(width, pushDepth, toothLength, fromTop: false, screenHeight: height);
        args.ScreenHandle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, CollectionsMarshal.AsSpan(_toothVerts));
    }

    /// <summary>
    /// Solid rectangle from the screen edge to pushDepth, covering the area
    /// the teeth vacated as they pushed in.
    /// </summary>
    private void BuildFill(float width, float pushDepth, bool fromTop, float screenHeight)
    {
        var near = fromTop ? 0f : screenHeight;
        var far = fromTop ? pushDepth : screenHeight - pushDepth;

        _fillVerts.Add(new Vector2(0f, near));
        _fillVerts.Add(new Vector2(width, near));
        _fillVerts.Add(new Vector2(0f, far));
        _fillVerts.Add(new Vector2(width, near));
        _fillVerts.Add(new Vector2(width, far));
        _fillVerts.Add(new Vector2(0f, far));
    }

    private void BuildToothRow(float width, float pushDepth, float toothLength, bool fromTop, float screenHeight)
    {
        var toothWidth = width / ToothCount;

        for (var i = 0; i < ToothCount; i++)
        {
            var jitteredLength = toothLength * (1f + _toothJitter[i % _toothJitter.Length]);
            var xLeft = i * toothWidth;
            var xRight = xLeft + toothWidth;
            var xMid = (xLeft + xRight) / 2f;

            var baseDepth = pushDepth;
            var tipDepth = pushDepth + jitteredLength;

            if (fromTop)
            {
                _toothVerts.Add(new DrawVertexUV2DColor(new Vector2(xLeft, baseDepth), Vector2.Zero, _baseColor));
                _toothVerts.Add(new DrawVertexUV2DColor(new Vector2(xRight, baseDepth), Vector2.Zero, _baseColor));
                _toothVerts.Add(new DrawVertexUV2DColor(new Vector2(xMid, tipDepth), Vector2.Zero, _tipColor));
            }
            else
            {
                _toothVerts.Add(new DrawVertexUV2DColor(new Vector2(xLeft, screenHeight - baseDepth), Vector2.Zero, _baseColor));
                _toothVerts.Add(new DrawVertexUV2DColor(new Vector2(xRight, screenHeight - baseDepth), Vector2.Zero, _baseColor));
                _toothVerts.Add(new DrawVertexUV2DColor(new Vector2(xMid, screenHeight - tipDepth), Vector2.Zero, _tipColor));
            }
        }
    }
}
