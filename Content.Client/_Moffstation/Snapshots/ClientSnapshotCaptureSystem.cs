using System.Numerics;
using Content.Client.Movement.Components;
using Content.Client.Movement.Systems;
using Content.Shared._Moffstation.Snapshots;
using Content.Shared.Camera;
using Content.Shared.Hands;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._Moffstation.Snapshots;

public sealed partial class ClientSnapshotCaptureSystem : SnapshotCaptureSystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private EyeCursorOffsetSystem _eyeOffset = default!;

    [Dependency] private EntityQuery<EyeCursorOffsetComponent> _cursorOffsetQuery = default!;

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

    protected override void OnAimedClick(Entity<SnapshotCaptureComponent> ent, EntityUid user, EntityCoordinates click)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var center = ClampToScreen(TransformSystem.ToMapCoordinates(click), ent.Comp.HalfExtents);
        RaisePredictiveEvent(new SnapshotCaptureRequestEvent(GetNetEntity(ent), GetNetCoordinates(TransformSystem.ToCoordinates(center))));
    }

    /// <summary>
    /// Moves <paramref name="center"/> so the whole frame stays on screen.
    /// </summary>
    public MapCoordinates ClampToScreen(MapCoordinates center, Vector2i halfExtents)
    {
        var view = _eye.GetWorldViewport();
        var rotation = GetFrame(center, halfExtents).Rotation;

        // Half the frame's size in world space, plus a tile for snapping the center to the tile grid.
        var half = new Vector2(halfExtents.X + 1f, halfExtents.Y + 1f);
        var cos = MathF.Abs((float) Math.Cos(rotation.Theta));
        var sin = MathF.Abs((float) Math.Sin(rotation.Theta));
        var worldHalf = new Vector2(cos * half.X + sin * half.Y, sin * half.X + cos * half.Y);

        var x = ClampAxis(center.Position.X, view.Left, view.Right, worldHalf.X);
        var y = ClampAxis(center.Position.Y, view.Bottom, view.Top, worldHalf.Y);
        return new MapCoordinates(new Vector2(x, y), center.MapId);
    }

    private static float ClampAxis(float value, float min, float max, float half)
    {
        // A screen smaller than the frame can't contain it; keep it centered then.
        if (max - min < half * 2)
            return (min + max) / 2;

        return Math.Clamp(value, min + half, max - half);
    }
}
