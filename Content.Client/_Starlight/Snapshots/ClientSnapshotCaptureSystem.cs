using System.Numerics;
using Content.Client.IconSmoothing;
using Content.Client.Movement.Components;
using Content.Client.Movement.Systems;
using Content.Shared._Starlight.Snapshots;
using Content.Shared.Camera;
using Content.Shared.Hands;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Snapshots;

/// <summary>
/// Client half of snapshot capture: clamps the frame to the screen and sends the shot request to the server.
/// The server validates and records the scene; this system only decides what the client aimed at.
/// </summary>
public sealed partial class ClientSnapshotCaptureSystem : SnapshotCaptureSystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private EyeCursorOffsetSystem _eyeOffset = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    [Dependency] private EntityQuery<EyeCursorOffsetComponent> _cursorOffsetQuery = default!;

    private readonly HashSet<EntityUid> _animationCandidates = [];

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new SnapshotCaptureViewfinderOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<SnapshotCaptureViewfinderOverlay>();
    }

    /// <summary>
    /// Like binoculars: while looking through the viewfinder, the view extends towards the cursor.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnGetEyeOffset(Entity<SnapshotCaptureComponent> ent, ref HeldRelayedEvent<GetEyeOffsetRelayedEvent> args)
    {
        if (!_cursorOffsetQuery.TryComp(ent, out var cursorOffset))
            return;

        if (!ent.Comp.Aiming)
        {
            // Start from the middle next time the viewfinder is raised.
            cursorOffset.CurrentPosition = Vector2.Zero;
            cursorOffset.TargetPosition = Vector2.Zero;
            return;
        }

        if (_eyeOffset.OffsetAfterMouse(ent, cursorOffset) is { } offset)
            args.Args.Offset += offset;
    }

    /// <summary>
    /// Sends the shot request for a click while looking through the viewfinder. Predicted once per click.
    /// </summary>
    protected override void OnAimedClick(
        Entity<SnapshotCaptureComponent> ent,
        EntityUid user,
        EntityCoordinates click,
        EntityUid? target)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var center = ClampToScreen(TransformSystem.ToMapCoordinates(click), ent.Comp.HalfExtents, out var frame);
        var animationFrames = GetAnimationFrames(center.MapId, frame, out var iconSmoothStates);
        NetEntity? focus = null;
        if (target is { } targetUid && TryGetNetEntity(targetUid, out var netTarget))
            focus = netTarget;

        RaisePredictiveEvent(new SnapshotCaptureRequestEvent(
            GetNetEntity(ent),
            GetNetCoordinates(TransformSystem.ToCoordinates(center)),
            _eye.CurrentEye.Rotation,
            animationFrames,
            iconSmoothStates,
            focus));
    }

    /// <summary>
    /// Collects capped animation frames and icon-smoothing states for sprites intersecting the frame.
    /// The server re-validates everything; this only bounds what the client sends.
    /// </summary>
    private List<SnapshotSpriteAnimation> GetAnimationFrames(
        MapId mapId,
        SnapshotFrame frame,
        out List<SnapshotSpriteLayerStateCapture> iconSmoothStates)
    {
        var frames = new List<SnapshotSpriteAnimation>();
        iconSmoothStates = [];
        _animationCandidates.Clear();
        _lookup.GetEntitiesIntersecting(mapId, TransformBounds(frame.Box, frame.FrameToWorld),
            _animationCandidates, LookupFlags.Uncontained);

        foreach (var uid in _animationCandidates)
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite)
                || !TryGetNetEntity(uid, out var netEntity)
                || netEntity is not { } netUid)
            {
                continue;
            }

            var coordinates = TransformSystem.GetMapCoordinates(uid);
            if (coordinates.MapId != mapId)
                continue;

            var local = Vector2.Transform(coordinates.Position, frame.WorldToFrame);
            var tile = new Vector2i((int) MathF.Floor(local.X), (int) MathF.Floor(local.Y));
            if (Math.Abs(tile.X) > frame.HalfExtents.X || Math.Abs(tile.Y) > frame.HalfExtents.Y)
                continue;

            var isIconSmooth = HasComp<IconSmoothComponent>(uid);
            var index = 0;
            foreach (var layer in sprite.AllLayers)
            {
                if (index >= MaxSnapshotAnimationLayers)
                    break;

                // Single RSI lookup shared by the smooth-state and animation checks.
                if (layer.ActualRsi is not { } rsi || !rsi.TryGetState(layer.RsiState, out var state))
                {
                    index++;
                    continue;
                }

                var stateName = layer.RsiState.Name;
                if (isIconSmooth
                    && iconSmoothStates.Count < MaxSnapshotIconSmoothStates
                    && !string.IsNullOrEmpty(stateName)
                    && stateName.Length <= MaxSnapshotIconSmoothStateLength)
                {
                    iconSmoothStates.Add(new SnapshotSpriteLayerStateCapture(netUid, index, stateName));
                }

                if (frames.Count < MaxSnapshotAnimationFrames && state.IsAnimated)
                {
                    frames.Add(new SnapshotSpriteAnimation(
                        netUid,
                        index,
                        Math.Clamp(layer.AnimationFrame, 0, state.DelayCount - 1)));
                }

                index++;
            }
        }

        return frames;
    }

    /// <summary>
    /// Moves <paramref name="center"/> so the whole frame stays on screen.
    /// </summary>
    public MapCoordinates ClampToScreen(MapCoordinates center, Vector2i halfExtents)
        => ClampToScreen(center, halfExtents, out _);

    /// <summary>
    /// Moves <paramref name="center"/> so the whole frame stays on screen.
    /// <paramref name="frame"/> receives the frame for the returned center.
    /// </summary>
    public MapCoordinates ClampToScreen(MapCoordinates center, Vector2i halfExtents, out SnapshotFrame frame)
    {
        var originalCenter = center;
        frame = GetFrame(center, halfExtents, _eye.CurrentEye.Rotation);
        var worldToFrame = frame.WorldToFrame;
        var view = _eye.GetWorldViewbounds();
        view.GetCorners(out var bottomLeft, out var bottomRight, out var topRight, out var topLeft);
        var min = new Vector2(float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity);
        IncludeViewportPoint(bottomLeft);
        IncludeViewportPoint(bottomRight);
        IncludeViewportPoint(topRight);
        IncludeViewportPoint(topLeft);

        // Half the frame's size in world space, plus a tile for snapping the center to the tile grid.
        var half = new Vector2(halfExtents.X + 1f, halfExtents.Y + 1f);

        var localCenter = Vector2.Transform(center.Position, frame.WorldToFrame);
        localCenter.X = ClampAxis(localCenter.X, min.X, max.X, half.X);
        localCenter.Y = ClampAxis(localCenter.Y, min.Y, max.Y, half.Y);
        center = new MapCoordinates(Vector2.Transform(localCenter, frame.FrameToWorld), center.MapId);
        if (center.Position != originalCenter.Position)
            frame = GetFrame(center, halfExtents, _eye.CurrentEye.Rotation);

        return center;

        void IncludeViewportPoint(Vector2 worldPoint)
        {
            var localPoint = Vector2.Transform(worldPoint, worldToFrame);
            min = Vector2.Min(min, localPoint);
            max = Vector2.Max(max, localPoint);
        }
    }

    /// <summary>
    /// Clamps an axis so the whole frame stays on screen. Keeps it centered when the viewport is smaller than the frame.
    /// </summary>
    private static float ClampAxis(float value, float min, float max, float half)
        // A screen smaller than the frame can't contain it; keep it centered then.
        => max - min < half * 2 ? (min + max) / 2 : Math.Clamp(value, min + half, max - half);
}
