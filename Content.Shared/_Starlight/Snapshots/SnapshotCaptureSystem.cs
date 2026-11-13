using System.Linq;
using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared._Starlight.Photography;
using Content.Shared.Flash;
using Content.Shared.Flash.Components;
using Content.Shared.Examine;
using Robust.Shared.Audio;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Timing;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Snapshots;

/// <summary>
/// Using the item in hand raises it to the eye; while raised, clicking takes a snapshot of the spot clicked on.
/// </summary>
/// <remarks>
/// Only the client knows where its screen ends, so it decides where the frame sits (clamped to the screen) and sends
/// that as a <see cref="SnapshotCaptureRequestEvent"/>. Everything else about the shot is checked here.
/// </remarks>
public abstract partial class SnapshotCaptureSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedFlashSystem _flash = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedContentEyeSystem _contentEye = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] protected SharedTransformSystem TransformSystem = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private EntityQuery<SnapshotCaptureComponent> _captureQuery = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private EntityQuery<FlashComponent> _flashQuery = default!;

    /// <summary>
    /// Upper bounds for client-sent animation data. Anything larger is ignored by the server.
    /// </summary>
    public const int MaxSnapshotAnimationFrames = 4096;
    public const int MaxSnapshotAnimationLayers = 32;
    public const int MaxSnapshotAnimationFrameIndex = 4095;
    public const int MaxSnapshotIconSmoothStates = 4096;
    public const int MaxSnapshotIconSmoothStateLength = 128;

    private static readonly VerbCategory _zoomCategory = new("snapshot-capture-zoom-category", null);

    [SubscribeLocalEvent]
    private void OnGetZoomVerbs(Entity<SnapshotCaptureComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (ent.Comp.ZoomLevels.Count == 0)
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

    /// <summary>
    /// Converts odd total frame dimensions to half extents from the center tile.
    /// Returns false when either dimension is non-positive or even.
    /// </summary>
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
    /// Melee hit photographs the hit target, like a viewfinder photo centred on its tile.
    /// If an inserted flash is present it flashes the target (spending the flash's charge);
    /// otherwise the photo is still taken, just dark.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnMeleeHit(Entity<SnapshotCaptureComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        if (TryComp<UseDelayComponent>(ent, out var shotDelay)
            && _useDelay.IsDelayed((ent.Owner, shotDelay)))
            return;

        var target = args.HitEntities.First();
        var user = args.User;
        var targetMapPos = TransformSystem.GetMapCoordinates(target);
        var center = targetMapPos;
        if (_map.TryFindGridAt(targetMapPos, out var gridUid, out var grid))
        {
            var tile = _map.WorldToTile(gridUid, grid, targetMapPos.Position);
            center = new MapCoordinates(_map.GridTileToWorldPos(gridUid, grid, tile), targetMapPos.MapId);
        }

        // Grid-aligned, like a normal photo viewed on the grid: frame-local axes = world + frame,
        // so use the negated grid rotation to make frame axes match the target tile's grid.
        var rotation = _map.TryFindGridAt(center, out var gridUid2, out var _)
            ? -TransformSystem.GetWorldRotation(gridUid2)
            : Angle.Zero;

        // Film cameras spend one film charge per photo.
        if (TryComp<AutoRechargeComponent>(ent, out _) == false
            && TryComp<LimitedChargesComponent>(ent, out var filmCharges)
            && !_charges.TryUseCharge((ent.Owner, filmCharges)))
        {
            return;
        }

        if (GetInsertedFlash(ent.Owner) is { } flashMeleeUid
            && _flashQuery.TryComp(flashMeleeUid, out var flashMelee))
        {
            if (_flash.TryUseFlashItem((flashMeleeUid, flashMelee), user))
            {
                _flash.Flash(target, user, flashMeleeUid, flashMelee.MeleeDuration, flashMelee.SlowTo, true, true, flashMelee.MeleeStunDuration);
                UpdateFlashAppearance(ent.Owner);
            }
        }

        _audio.PlayPredicted(ent.Comp.ShutterSound, ent.Owner, user);

        if (TryComp<UseDelayComponent>(ent, out var useDelay))
            _useDelay.TryResetDelay((ent.Owner, useDelay));

        var captured = new SnapshotCapturedEvent(
            user,
            center,
            rotation,
            null,
            null,
            target);
        RaiseLocalEvent(ent.Owner, ref captured);
    }

    [SubscribeLocalEvent]
    private void OnFlashInserted(Entity<SnapshotCaptureComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (HasComp<FlashComponent>(args.Entity))
            UpdateFlashAppearance(ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnFlashRemoved(Entity<SnapshotCaptureComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (HasComp<FlashComponent>(args.Entity))
            UpdateFlashAppearance(ent.Owner);
    }

    /// <summary>
    /// Mirrors the inserted flash state onto the camera icon, refreshing when a flash is inserted or removed.
    /// </summary>
    private void UpdateFlashAppearance(EntityUid capturer)
    {
        var flashUid = GetInsertedFlash(capturer);
        _appearance.SetData(
            capturer,
            FlashVisuals.Burnt,
            flashUid is { } inserted && _charges.GetCurrentCharges((inserted, null, null)) == 0);
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<SnapshotCaptureComponent> ent, ref ExaminedEvent args)
    {
        if (GetInsertedFlash(ent.Owner) is { } flashUid)
        {
            args.PushMarkup(Loc.GetString("snapshot-examine-flash", ("charges", _charges.GetCurrentCharges((flashUid, null, null)))));
        }
    }

    [SubscribeLocalEvent]
    private void OnInteractUsing(Entity<SnapshotCaptureComponent> ent, ref InteractUsingEvent args)
    {
        // Skip client prediction: deleting a networked blank there desyncs.
        if (!_net.IsServer)
            return;

        if (!HasComp<BlankPhotographComponent>(args.Used))
            return;

        if (TryComp<AutoRechargeComponent>(ent, out _))
            return;

        if (!TryComp<LimitedChargesComponent>(ent, out var charges))
            return;

        var before = _charges.GetCurrentCharges((ent.Owner, charges, null));
        if (before >= charges.MaxCharges)
            return;

        _charges.AddCharges((ent.Owner, charges, null), 1);
        args.Handled = true;
        Del(args.Used);
        _popup.PopupClient(Loc.GetString("snapshot-film-loaded", ("charges", _charges.GetCurrentCharges((ent.Owner, charges, null))), ("max", charges.MaxCharges)), args.User, args.User);
    }

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

    /// <summary>
    /// Finds the inserted flash (if any) on the capturer via its item slots.
    /// </summary>
    private EntityUid? GetInsertedFlash(EntityUid capturer)
    {
        if (_itemSlots.GetItemOrNull(capturer, "flash") is { } flashUid
            && _flashQuery.TryComp(flashUid, out _))
        {
            return flashUid;
        }

        return null;
    }

    private bool FireFlashEntity(EntityUid flashUid, FlashComponent flash, EntityUid _, EntityUid? user)
    {
        if (!_flash.TryUseFlashItem((flashUid, flash), user))
            return false;

        _flash.FlashArea(flashUid, user, flash.Range, flash.AoeFlashDuration, flash.SlowTo, true, flash.Probability);
        return true;
    }

    public bool HasInsertedFlash(EntityUid capturer) => GetInsertedFlash(capturer) != null;

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

        if (!_actionBlocker.CanInteract(user, capturer)
            || !_actionBlocker.CanUseHeldEntity(user, capturer))
        {
            return;
        }

        if (TryComp<UseDelayComponent>(capturer, out var shotDelay)
            && _useDelay.IsDelayed((capturer, shotDelay)))
            return;

        var rotation = new Angle(Math.IEEERemainder(ev.Rotation.Theta, Math.PI * 2));
        EntityUid? immediateFocus = null;
        if (ev.Focus is { } focusNet
            && TryGetEntity(focusNet, out var focusEntity)
            && focusEntity is { } focusUid)
        {
            immediateFocus = focusUid;
        }

        FireShot(user, capturer, center, rotation, immediateFocus, ev.AnimationFrames, ev.IconSmoothStates, capture.ShutterSound);
    }

    private void FireShot(EntityUid user, EntityUid capturer, MapCoordinates center, Angle rotation, EntityUid? focus, List<SnapshotSpriteAnimation>? animationFrames, List<SnapshotSpriteLayerStateCapture>? iconSmoothStates, SoundSpecifier shutterSound)
    {
        // Film cameras (no AutoRecharge) spend one film charge per photo.
        var filmSpent = false;
        if (TryComp<AutoRechargeComponent>(capturer, out _) == false
            && TryComp<LimitedChargesComponent>(capturer, out var filmCharges)
            && _charges.TryUseCharge((capturer, filmCharges)))
        {
            filmSpent = true;
        }
        else if (TryComp<AutoRechargeComponent>(capturer, out _) == false
            && TryComp<LimitedChargesComponent>(capturer, out _))
        {
            _popup.PopupClient(Loc.GetString("snapshot-film-needs-blank"), user, user);
            return;
        }

        void RefundFilm()
        {
            if (!filmSpent)
                return;

            if (TryComp<LimitedChargesComponent>(capturer, out var refundCharges))
                _charges.AddCharges((capturer, refundCharges, null), 1);
        }

        if (GetInsertedFlash(capturer) is { } insertedFlash
            && _flashQuery.TryComp(insertedFlash, out var insertedComp))
        {
            if (!FireFlashEntity(insertedFlash, insertedComp, capturer, user))
            {
                RefundFilm();
                _popup.PopupClient(Loc.GetString("snapshot-capture-rate-limited"), user, user);
                return;
            }

            UpdateFlashAppearance(capturer);
        }
        else if (_flashQuery.TryComp(capturer, out var builtInFlash))
        {
            if (!FireFlashEntity(capturer, builtInFlash, capturer, user))
            {
                RefundFilm();
                _popup.PopupClient(Loc.GetString("snapshot-capture-rate-limited"), user, user);
                return;
            }
        }

        _audio.PlayPredicted(shutterSound, capturer, user);

        if (TryComp<UseDelayComponent>(capturer, out var useDelay))
            _useDelay.TryResetDelay((capturer, useDelay));

        var captured = new SnapshotCapturedEvent(
            user,
            center,
            rotation,
            animationFrames,
            iconSmoothStates,
            focus);
        RaiseLocalEvent(capturer, ref captured);
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
    /// Builds the camera-aligned frame for a shot: the center snapped to its tile, with forward/inverse
    /// world-to-frame matrices. Shared by client clamping and server capture so both agree on the frame.
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

    /// <summary>
    /// Transforms a frame-local box to a world-aligned box, for entity lookups intersecting the frame.
    /// </summary>
    protected static Box2 TransformBounds(Box2 box, Matrix3x2 transform)
    {
        var point = Vector2.Transform(box.BottomLeft, transform);
        var min = point;
        var max = point;

        point = Vector2.Transform(box.BottomRight, transform);
        min = Vector2.Min(min, point);
        max = Vector2.Max(max, point);

        point = Vector2.Transform(box.TopLeft, transform);
        min = Vector2.Min(min, point);
        max = Vector2.Max(max, point);

        point = Vector2.Transform(box.TopRight, transform);
        min = Vector2.Min(min, point);
        max = Vector2.Max(max, point);

        return new Box2(min, max);
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
