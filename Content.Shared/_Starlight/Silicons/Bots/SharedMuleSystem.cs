using System.Diagnostics.CodeAnalysis;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Mobs.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Starlight.Silicons.Bots;

public abstract partial class SharedMuleSystem : EntitySystem
{
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] protected SharedContainerSystem Container = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    [SubscribeLocalEvent]
    private void OnInit(Entity<MuleComponent> ent, ref ComponentInit args)
    {
        Container.EnsureContainer<ContainerSlot>(ent, ent.Comp.ContainerId);
    }

    [SubscribeLocalEvent]
    private void OnCanDropTarget(Entity<MuleComponent> ent, ref CanDropTargetEvent args)
    {
        args.CanDrop |= CanLoad(ent, args.Dragged);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnDragDropTarget(Entity<MuleComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!TryLoad(ent, args.Dragged))
            Audio.PlayPredicted(ent.Comp.SighSound, ent, args.User);
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<MuleComponent> ent, ref ExaminedEvent args)
    {
        if (TryGetLoad(ent, out var load))
            args.PushMarkup(Loc.GetString("mule-examine-load", ("load", load)));
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

        if (!_whitelist.IsWhitelistPassOrNull(ent.Comp.Whitelist, item))
            return false;

        // Nobody gets shut in a crate and driven off.
        if (TryComp<EntityStorageComponent>(item, out var storage))
        {
            foreach (var contained in storage.Contents.ContainedEntities)
            {
                if (HasComp<MobStateComponent>(contained))
                    return false;
            }
        }

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
    /// Drops whatever it's carrying on the free tile ahead of it, or behind or beside it if that's blocked.
    /// </summary>
    public bool Unload(Entity<MuleComponent> ent)
    {
        if (!TryGetLoad(ent, out var load))
            return false;

        Container.Remove(load.Value, Container.GetContainer(ent, ent.Comp.ContainerId));
        if (DropSpot(ent) is { } spot)
            _transform.SetCoordinates(load.Value, spot);

        Audio.PlayPredicted(ent.Comp.LoadSound, ent, null);
        return true;
    }

    private EntityCoordinates? DropSpot(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return null;

        var tile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        var facing = xform.LocalRotation.GetCardinalDir();
        foreach (var dir in new[] { facing, facing.GetOpposite(), facing.GetClockwise90Degrees(), facing.GetOpposite().GetClockwise90Degrees() })
        {
            var next = tile.Offset(dir);
            if (_map.TryGetTileRef(gridUid, grid, next, out var tileRef) && !tileRef.Tile.IsEmpty && !_turf.IsTileBlocked(gridUid, next, CollisionGroup.MidImpassable, grid))
                return _map.GridTileToLocal(gridUid, grid, next);
        }

        return null;
    }

    public bool TryGetLoad(Entity<MuleComponent> ent, [NotNullWhen(true)] out EntityUid? load)
    {
        load = null;
        if (!Container.TryGetContainer(ent, ent.Comp.ContainerId, out var container) || container.ContainedEntities.Count == 0)
            return false;

        load = container.ContainedEntities[0];
        return true;
    }

    protected void SetOn(Entity<MuleComponent> ent, bool on)
    {
        ent.Comp.On = on;
        if (!on)
            ent.Comp.Mode = MuleMode.Idle;

        _appearance.SetData(ent, MuleVisuals.On, on);
        Dirty(ent);
    }
}
