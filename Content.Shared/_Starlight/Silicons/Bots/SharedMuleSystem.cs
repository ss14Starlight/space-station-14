using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Content.Shared.Wires;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Silicons.Bots;

public abstract partial class SharedMuleSystem : EntitySystem
{
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] protected SharedContainerSystem Container = default!;
    [Dependency] protected SharedAppearanceSystem Appearance = default!;
    [Dependency] protected SharedBuckleSystem Buckle = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected SharedWiresSystem Wires = default!;
    [Dependency] protected MovementSpeedModifierSystem Movement = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    private static readonly ProtoId<TagPrototype> CrateTag = "Crate";

    [SubscribeLocalEvent]
    private void OnInit(Entity<MuleComponent> ent, ref ComponentInit args)
    {
        Container.EnsureContainer<ContainerSlot>(ent, ent.Comp.ContainerId);
    }

    [SubscribeLocalEvent]
    private void OnCrateCanDrag(Entity<EntityStorageComponent> ent, ref CanDragEvent args)
    {
        // Crates can be dragged so they can be dropped onto a MULE.
        if (_tag.HasTag(ent, CrateTag))
            args.Handled = true;
    }

    // After the buckle system, which sets CanDrop outright for its strap.
    [SubscribeLocalEvent(after: [typeof(SharedBuckleSystem)])]
    private void OnCanDropTarget(Entity<MuleComponent> ent, ref CanDropTargetEvent args)
    {
        // Anything can be offered; it buzzes at what it won't take.
        args.CanDrop |= args.Dragged != ent.Owner;
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnDragDropTarget(Entity<MuleComponent> ent, ref DragDropTargetEvent args)
    {
        // People are strapped on by the buckle system, if the load check lets them.
        if (args.Handled || HasComp<BuckleComponent>(args.Dragged) && HasWireCut(ent, MuleWires.LoadCheck))
            return;

        args.Handled = true;
        if (!TryLoad(ent, args.Dragged))
            Audio.PlayPredicted(ent.Comp.SighSound, ent, args.User);
    }

    [SubscribeLocalEvent]
    private void OnStrapAttempt(Entity<MuleComponent> ent, ref StrapAttemptEvent args)
    {
        // Only with the load check cut, and a rider takes the place of a load.
        if (!HasWireCut(ent, MuleWires.LoadCheck) || TryGetLoad(ent, out _))
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnCellInsertAttempt(Entity<MuleComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        // The cell sits behind the maintenance panel.
        if (args.User != null && !Wires.IsPanelOpen(ent.Owner))
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnCellEjectAttempt(Entity<MuleComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.User != null && !Wires.IsPanelOpen(ent.Owner))
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnRefreshSpeed(Entity<MuleComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var motor = (HasWireCut(ent, MuleWires.Motor1), HasWireCut(ent, MuleWires.Motor2)) switch
        {
            (true, true) => 0f,
            (true, false) => ent.Comp.FastMotor,
            (false, true) => ent.Comp.MediumMotor,
            _ => 1f,
        };

        if (ent.Comp.CarefulUntil > Timing.CurTime)
            motor *= ent.Comp.CarefulSlowdown;

        args.ModifySpeed(motor);
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<MuleComponent> ent, ref ExaminedEvent args)
    {
        if (TryGetLoad(ent, out var load))
            args.PushMarkup(Loc.GetString("mule-examine-load", ("load", load)));
    }

    public static bool HasWireCut(Entity<MuleComponent> ent, MuleWires wire)
    {
        return (ent.Comp.CutWires & wire) != 0;
    }

    /// <summary>
    /// Whether it could carry this right now.
    /// </summary>
    public bool CanLoad(Entity<MuleComponent> ent, EntityUid item)
    {
        if (TryGetLoad(ent, out _) || item == ent.Owner)
            return false;

        if (Transform(item).Anchored || Container.IsEntityInContainer(item))
            return false;

        // With the load check cut it takes any loose object, not just crates. People ride, they aren't loaded.
        if (HasComp<MobStateComponent>(item))
            return false;

        if (!HasWireCut(ent, MuleWires.LoadCheck) && !_whitelist.IsWhitelistPassOrNull(ent.Comp.Whitelist, item))
            return false;

        // Nobody gets shut in something and driven off, or strapped to it.
        if (TryComp<EntityStorageComponent>(item, out var storage) && storage.Contents.ContainedEntities.Any(HasComp<MobStateComponent>))
            return false;

        if (TryComp<StrapComponent>(item, out var strap) && strap.BuckledEntities.Count > 0)
            return false;

        return true;
    }

    /// <summary>
    /// Puts something on the load platform, closing it first if it's a crate.
    /// </summary>
    public bool TryLoad(Entity<MuleComponent> ent, EntityUid item)
    {
        if (!CanLoad(ent, item))
            return false;

        if (TryComp<EntityStorageComponent>(item, out var storage) && storage.Open && !_entityStorage.TryCloseStorage(item))
            return false;

        if (!Container.Insert(item, Container.GetContainer(ent, ent.Comp.ContainerId)))
            return false;

        Audio.PlayPredicted(ent.Comp.LoadSound, ent, null);
        return true;
    }

    /// <summary>
    /// Drops whatever it's carrying on the tile ahead of it, or under itself if that's blocked. A rider just gets let off.
    /// </summary>
    public bool Unload(Entity<MuleComponent> ent)
    {
        if (TryGetRider(ent, out var rider))
        {
            Buckle.Unbuckle(rider.Value, null);
            return true;
        }

        if (!TryGetLoad(ent, out var load))
            return false;

        Container.Remove(load.Value, Container.GetContainer(ent, ent.Comp.ContainerId));
        if (AheadSpot(ent) is { } spot)
        {
            _transform.SetCoordinates(load.Value, spot);
        }
        else
        {
            // Left under it, so it can still back out of a tight spot; it ignores the load until it's clear of it.
            var ignore = EnsureComp<PreventCollideComponent>(load.Value);
            ignore.Uid = ent;
            Dirty(load.Value, ignore);
            ent.Comp.DroppedUnder = load;
        }

        Audio.PlayPredicted(ent.Comp.LoadSound, ent, null);
        return true;
    }

    private EntityCoordinates? AheadSpot(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return null;

        var ahead = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates).Offset(xform.LocalRotation.GetCardinalDir());
        if (!_map.TryGetTileRef(gridUid, grid, ahead, out var tileRef) || tileRef.Tile.IsEmpty
            || _turf.IsTileBlocked(gridUid, ahead, CollisionGroup.MidImpassable, grid))
            return null;

        return _map.GridTileToLocal(gridUid, grid, ahead);
    }

    /// <summary>
    /// Once it has moved off a load it dropped under itself, the two collide again.
    /// </summary>
    protected void CheckDroppedUnder(Entity<MuleComponent> ent)
    {
        if (ent.Comp.DroppedUnder is not { } load)
            return;

        if (!TerminatingOrDeleted(load) && Container.IsEntityInContainer(load) == false
            && (_transform.GetWorldPosition(load) - _transform.GetWorldPosition(ent)).Length() < 1f)
            return;

        if (TryComp<PreventCollideComponent>(load, out var ignore) && ignore.Uid == ent.Owner)
            RemComp(load, ignore);

        ent.Comp.DroppedUnder = null;
    }

    /// <summary>
    /// What it's carrying: the thing on its platform, or whoever is riding it.
    /// </summary>
    public bool TryGetLoad(Entity<MuleComponent> ent, [NotNullWhen(true)] out EntityUid? load)
    {
        load = null;
        if (TryGetRider(ent, out load))
            return true;

        if (!Container.TryGetContainer(ent, ent.Comp.ContainerId, out var container) || container.ContainedEntities.Count == 0)
            return false;

        load = container.ContainedEntities[0];
        return true;
    }

    public bool TryGetRider(Entity<MuleComponent> ent, [NotNullWhen(true)] out EntityUid? rider)
    {
        rider = null;
        if (!TryComp<StrapComponent>(ent, out var strap) || strap.BuckledEntities.Count == 0)
            return false;

        rider = strap.BuckledEntities.First();
        return true;
    }

    protected void SetOn(Entity<MuleComponent> ent, bool on)
    {
        ent.Comp.On = on;
        _light.SetEnabled(ent, on);
        Dirty(ent);
    }
}
