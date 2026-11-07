using System.Numerics;
using Content.Shared.Flash;
using Content.Shared.Flash.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Snapshots;

/// <summary>
/// Using the item in hand raises it to the eye when raised you can take a snapshot of a spot you click on.
/// </summary>
/// <remarks>
/// Only the client knows where its screen ends, so it decides where the frame sits (clamped to the screen) and sends
/// that as a <see cref="SnapshotCaptureRequestEvent"/>. Everything else about the shot is checked here.
/// </remarks>
public abstract partial class SnapshotCaptureSystem : EntitySystem
{
    [Dependency] private SharedFlashSystem _flash = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedContentEyeSystem _contentEye = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] protected SharedTransformSystem TransformSystem = default!;
    [Dependency] private EntityQuery<SnapshotCaptureComponent> _captureQuery = default!;
    [Dependency] private EntityQuery<FlashComponent> _flashQuery = default!;

    public const int MaxSnapshotAnimationFrames = 4096;
    public const int MaxSnapshotAnimationLayers = 32;
    public const int MaxSnapshotAnimationFrameIndex = 4095;
    public const int MaxSnapshotIconSmoothStates = 4096;
    public const int MaxSnapshotIconSmoothStateLength = 128;

    private static readonly VerbCategory _zoomCategory = new("snapshot-capture-zoom-category", null);

    [SubscribeLocalEvent]
    private void OnGetZoomVerbs(Entity<SnapshotCaptureComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (ent.Comp.ZoomLevels.Count == 0
            || !args.CanInteract
            || !args.CanAccess)
        {
            return;
        }

        for (var i = 0; i < ent.Comp.ZoomLevels.Count; i++)
        {
            var dimensions = ent.Comp.ZoomLevels[i];
            if (!TryGetHalfExtents(dimensions, out var halfExtents))
            {
                Log.Error($"Invalid snapshot zoom dimensions {dimensions} on {ToPrettyString(ent.Owner)}; dimensions must be positive odd numbers.");
                continue;
            }

            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("snapshot-capture-zoom-option",
                    ("width", dimensions.X),
                    ("height", dimensions.Y)),
                Disabled = ent.Comp.HalfExtents == halfExtents,
                Priority = -i,
                Category = _zoomCategory,
                Act = () => SetZoom(ent.AsNullable(), dimensions),
            });
        }
    }

    private static bool TryGetHalfExtents(Vector2i dimensions, out Vector2i halfExtents)
    {
        halfExtents = default;
        if (dimensions.X < 1 || dimensions.Y < 1 || dimensions.X % 2 == 0 || dimensions.Y % 2 == 0)
            return false;

        halfExtents = new Vector2i((dimensions.X - 1) / 2, (dimensions.Y - 1) / 2);
        return true;
    }

    private void SetZoom(Entity<SnapshotCaptureComponent?> ent, Vector2i dimensions)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (!TryGetHalfExtents(dimensions, out var halfExtents)
            || !ent.Comp.ZoomLevels.Contains(dimensions))
        {
            Log.Warning($"Rejected unconfigured snapshot zoom level {dimensions} for {ToPrettyString(ent.Owner)}.");
            return;
        }

        if (ent.Comp.HalfExtents == halfExtents)
            return;

        ent.Comp.HalfExtents = halfExtents;
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnUseInHand(Entity<SnapshotCaptureComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        SetAiming(ent.AsNullable(), !ent.Comp.Aiming, args.User);
        _popup.PopupClient(Loc.GetString(ent.Comp.Aiming ? "snapshot-capture-raise" : "snapshot-capture-lower"), args.User, args.User);
    }

    [SubscribeLocalEvent]
    private void OnUnequipped(Entity<SnapshotCaptureComponent> ent, ref GotUnequippedHandEvent args)
        => SetAiming(ent.AsNullable(), false, args.User);

    /// <summary>
    /// Switching to the other hand lowers the viewfinder, so it isn't still raised when switching back.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnDeselected(Entity<SnapshotCaptureComponent> ent, ref HandDeselectedEvent args)
    {
        if (!ent.Comp.Aiming)
            return;

        SetAiming(ent.AsNullable(), false, args.User);
    }

    /// <remarks>
    /// Raised for every click with the item in hand, near or far, so handling it here keeps clicks from also
    /// interacting with whatever was clicked. The shot itself comes from the client's <see cref="SnapshotCaptureRequestEvent"/>.
    /// </remarks>
    [SubscribeLocalEvent]
    private void OnBeforeInteract(Entity<SnapshotCaptureComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled || !ent.Comp.Aiming)
            return;

        args.Handled = true;
        OnAimedClick(ent, args.User, args.ClickLocation, args.Target);
    }

    /// <summary>
    /// A click while looking through the viewfinder. The client turns this into a shot request.
    /// </summary>
    protected virtual void OnAimedClick(
        Entity<SnapshotCaptureComponent> ent,
        EntityUid user,
        EntityCoordinates click,
        EntityUid? target)
    {
    }

    [EventSubscription]
    private void OnCaptureRequest(SnapshotCaptureRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user
            || !TryGetEntity(ev.Capturer, out var capturerUid)
            || capturerUid is not { } capturer
            || !_captureQuery.TryComp(capturer, out var capture)
            || !capture.Aiming
            || _hands.GetActiveItem(user) != capturer)
        {
            return;
        }

        if (!double.IsFinite(ev.Rotation.Theta))
        {
            Log.Warning($"Rejected snapshot capture from {ToPrettyString(user)} with a non-finite view rotation.");
            return;
        }

        // The center comes from the client, so the range is enforced here.
        var centerCoordinates = GetCoordinates(ev.Center);
        if (!TransformSystem.IsValid(centerCoordinates))
        {
            Log.Warning($"Rejected snapshot capture from {ToPrettyString(user)} with an invalid center.");
            return;
        }

        var center = TransformSystem.ToMapCoordinates(centerCoordinates);
        var from = TransformSystem.GetMapCoordinates(user);
        if (center.MapId != from.MapId || (center.Position - from.Position).Length() > capture.MaxRange)
        {
            _popup.PopupClient(Loc.GetString("snapshot-capture-out-of-range"), user, user);
            return;
        }

        if (_flashQuery.TryComp(capturerUid, out var flash))
        {
            // Uses a charge, respects the use delay and plays the shutter sound.
            if (!_flash.TryUseFlashItem((capturerUid.Value, flash), user))
                return;

            _flash.FlashArea(capturerUid.Value, user, flash.Range, flash.AoeFlashDuration, flash.SlowTo, true, flash.Probability);
        }

        var rotation = new Angle(Math.IEEERemainder(ev.Rotation.Theta, Math.PI * 2));
        EntityUid? focus = null;
        if (ev.Focus is { } focusNet
            && TryGetEntity(focusNet, out var focusEntity)
            && focusEntity is { } focusUid)
        {
            focus = focusUid;
        }

        var captured = new SnapshotCapturedEvent(
            user,
            center,
            rotation,
            ev.AnimationFrames,
            ev.IconSmoothStates,
            focus);
        RaiseLocalEvent(capturerUid.Value, ref captured);
    }

    /// <summary>
    /// Sets the viewfinder aiming state. Marks the component dirty and updates the user's PVS scale when a user is given.
    /// </summary>
    public void SetAiming(Entity<SnapshotCaptureComponent?> ent, bool aiming, EntityUid? user)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (ent.Comp.Aiming == aiming)
            return;

        ent.Comp.Aiming = aiming;
        Dirty(ent);

        // Looking through the viewfinder extends the view, which needs a larger PVS range.
        if (user != null)
            _contentEye.UpdatePvsScale(user.Value);
    }

    /// <summary>
    /// Ensures the Snapshot is what the server shows
    /// </summary>
    public SnapshotFrame GetFrame(MapCoordinates center, Vector2i halfExtents, Angle rotation)
    {
        // Eye.Rotation rotates the world into screen space, so camera-local coordinates map back to world
        // with the inverse rotation.
        var frameToWorld = Matrix3x2.CreateRotation((float) -rotation.Theta);
        Vector2 snappedCenter;
        if (_map.TryFindGridAt(center, out var gridUid, out var grid))
        {
            var tile = _map.WorldToTile(gridUid, grid, center.Position);
            snappedCenter = _map.GridTileToWorldPos(gridUid, grid, tile);
        }
        else
        {
            Matrix3x2.Invert(frameToWorld, out var worldToFrame);

            var local = Vector2.Transform(center.Position, worldToFrame);
            var tileCenter = new Vector2(
                MathF.Floor(local.X) + 0.5f,
                MathF.Floor(local.Y) + 0.5f);
            snappedCenter = Vector2.Transform(tileCenter, frameToWorld);
        }

        var rotatedCenter = Vector2.Transform(new Vector2(0.5f), frameToWorld);
        frameToWorld.M31 = snappedCenter.X - rotatedCenter.X;
        frameToWorld.M32 = snappedCenter.Y - rotatedCenter.Y;
        Matrix3x2.Invert(frameToWorld, out var snappedWorldToFrame);
        return new SnapshotFrame(Vector2i.Zero, halfExtents, rotation, frameToWorld, snappedWorldToFrame);
    }
}

/// <summary>
/// Sent by the client to take a shot centered on <see cref="Center"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class SnapshotCaptureRequestEvent(
    NetEntity capturer,
    NetCoordinates center,
    Angle rotation,
    List<SnapshotSpriteAnimation>? animationFrames,
    List<SnapshotSpriteLayerStateCapture>? iconSmoothStates,
    NetEntity? focus) : EntityEventArgs
{
    public readonly NetEntity Capturer = capturer;
    public readonly NetCoordinates Center = center;
    public readonly Angle Rotation = rotation;
    public readonly List<SnapshotSpriteAnimation>? AnimationFrames = animationFrames;
    public readonly List<SnapshotSpriteLayerStateCapture>? IconSmoothStates = iconSmoothStates;
    public readonly NetEntity? Focus = focus;
}

public readonly record struct SnapshotFrame(
    Vector2i CenterTile,
    Vector2i HalfExtents,
    Angle Rotation,
    Matrix3x2 FrameToWorld,
    Matrix3x2 WorldToFrame)
{
    /// <summary>
    /// The covered area in frame-local coordinates.
    /// </summary>
    public Box2 Box => new(CenterTile - HalfExtents, CenterTile + HalfExtents + Vector2i.One);
}
