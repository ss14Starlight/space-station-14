using System.Numerics;
using Content.Shared.Flash;
using Content.Shared.Flash.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.Snapshots;

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
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedContentEyeSystem _contentEye = default!;
    [Dependency] protected SharedTransformSystem TransformSystem = default!;

    [Dependency] private EntityQuery<SnapshotCaptureComponent> _captureQuery = default!;
    [Dependency] private EntityQuery<FlashComponent> _flashQuery = default!;

    [SubscribeLocalEvent]
    private void OnUseInHand(Entity<SnapshotCaptureComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        SetAiming(ent, !ent.Comp.Aiming, args.User);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Aiming ? "snapshot-capture-raise" : "snapshot-capture-lower"), args.User, args.User);
    }

    [SubscribeLocalEvent]
    private void OnUnequipped(Entity<SnapshotCaptureComponent> ent, ref GotUnequippedHandEvent args)
    {
        SetAiming(ent, false, args.User);
    }

    /// <summary>
    /// Switching to the other hand lowers the viewfinder, so it isn't still raised when switching back.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnDeselected(Entity<SnapshotCaptureComponent> ent, ref HandDeselectedEvent args)
    {
        if (!ent.Comp.Aiming)
            return;

        SetAiming(ent, false, args.User);
        _popup.PopupEntity(Loc.GetString("snapshot-capture-lower"), args.User, args.User);
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
        OnAimedClick(ent, args.User, args.ClickLocation);
    }

    /// <summary>
    /// A click while looking through the viewfinder. The client turns this into a shot request.
    /// </summary>
    protected virtual void OnAimedClick(Entity<SnapshotCaptureComponent> ent, EntityUid user, EntityCoordinates click)
    {
    }

    [EventSubscription]
    private void OnCaptureRequest(SnapshotCaptureRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user
            || !TryGetEntity(ev.Capturer, out var capturerUid)
            || !_captureQuery.TryComp(capturerUid, out var capture)
            || !capture.Aiming
            || _hands.GetActiveItem(user) != capturerUid)
        {
            return;
        }

        // The center comes from the client, so the range is enforced here.
        var center = TransformSystem.ToMapCoordinates(GetCoordinates(ev.Center));
        var from = TransformSystem.GetMapCoordinates(user);
        if (center.MapId != from.MapId || (center.Position - from.Position).Length() > capture.MaxRange)
        {
            _popup.PopupEntity(Loc.GetString("snapshot-capture-out-of-range"), user, user);
            return;
        }

        if (_flashQuery.TryComp(capturerUid, out var flash))
        {
            // Uses a charge, respects the use delay and plays the shutter sound.
            if (!_flash.TryUseFlashItem((capturerUid.Value, flash), user))
                return;

            _flash.FlashArea(capturerUid.Value, user, flash.Range, flash.AoeFlashDuration, flash.SlowTo, true, flash.Probability);
        }

        var captured = new SnapshotCapturedEvent(user, center);
        RaiseLocalEvent(capturerUid.Value, ref captured);
    }

    public void SetAiming(Entity<SnapshotCaptureComponent> ent, bool aiming, EntityUid? user)
    {
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
    public SnapshotFrame GetFrame(MapCoordinates center, Vector2i halfExtents)
    {
        EntityUid? grid = null;
        var rotation = Angle.Zero;
        var frameToWorld = Matrix3x2.Identity;
        var worldToFrame = Matrix3x2.Identity;
        if (_map.TryFindGridAt(center, out var gridUid, out _))
        {
            grid = gridUid;
            (_, rotation, frameToWorld, worldToFrame) = TransformSystem.GetWorldPositionRotationMatrixWithInv(gridUid);
        }

        var local = Vector2.Transform(center.Position, worldToFrame);
        var centerTile = new Vector2i((int) MathF.Floor(local.X), (int) MathF.Floor(local.Y));
        return new SnapshotFrame(grid, centerTile, halfExtents, rotation, frameToWorld, worldToFrame);
    }
}

/// <summary>
/// Sent by the client to take a shot centered on <see cref="Center"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class SnapshotCaptureRequestEvent(NetEntity capturer, NetCoordinates center) : EntityEventArgs
{
    public readonly NetEntity Capturer = capturer;
    public readonly NetCoordinates Center = center;
}

public readonly record struct SnapshotFrame(
    EntityUid? Grid,
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
