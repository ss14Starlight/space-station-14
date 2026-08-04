using Content.Server.Popups;
using Content.Server.Storage.EntitySystems;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Gives Bioseal occupants a short, unlocked escape action while leaving external
/// zip and unzip handling to the standard entity-storage interaction.
/// </summary>
public sealed partial class BiosealRollerbedSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityStorageSystem _storage = default!;
    [Dependency] private PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BiosealRollerbedComponent, ContainerRelayMovementEntityEvent>(OnRelayMovement);
        SubscribeLocalEvent<BiosealRollerbedComponent, BiosealInternalUnzipDoAfterEvent>(OnInternalUnzip);
        SubscribeLocalEvent<BiosealRollerbedComponent, StorageAfterOpenEvent>(OnOpened);
        SubscribeLocalEvent<BiosealRollerbedComponent, StorageAfterCloseEvent>(OnClosed);
    }

    private void OnRelayMovement(
        Entity<BiosealRollerbedComponent> entity,
        ref ContainerRelayMovementEntityEvent args)
    {
        if (entity.Comp.InternalUnzipDoAfter is not null ||
            !_actionBlocker.CanMove(args.Entity) ||
            !TryComp<EntityStorageComponent>(entity, out var storage) ||
            storage.Open ||
            !storage.Contents.Contains(args.Entity))
        {
            return;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.Entity,
            entity.Comp.InternalUnzipTime,
            new BiosealInternalUnzipDoAfterEvent(),
            entity,
            target: entity)
        {
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter, out entity.Comp.InternalUnzipDoAfter))
            return;

        _popup.PopupEntity(
            Loc.GetString("bioseal-rollerbed-internal-unzip-start"),
            args.Entity,
            args.Entity,
            PopupType.SmallCaution);
    }

    private void OnInternalUnzip(
        Entity<BiosealRollerbedComponent> entity,
        ref BiosealInternalUnzipDoAfterEvent args)
    {
        entity.Comp.InternalUnzipDoAfter = null;

        if (args.Cancelled || args.Handled ||
            args.Target is not { } target ||
            !TryComp<EntityStorageComponent>(entity, out var storage) ||
            storage.Open ||
            !storage.Contents.Contains(args.User))
        {
            return;
        }

        args.Handled = _storage.TryOpenStorage(args.User, target);
    }

    private void OnOpened(Entity<BiosealRollerbedComponent> entity, ref StorageAfterOpenEvent args)
    {
        if (entity.Comp.InternalUnzipDoAfter is { } doAfter)
            _doAfter.Cancel(doAfter);

        entity.Comp.InternalUnzipDoAfter = null;
        _popup.PopupEntity(Loc.GetString("bioseal-rollerbed-unzipped"), entity);
    }

    private void OnClosed(Entity<BiosealRollerbedComponent> entity, ref StorageAfterCloseEvent args)
        => _popup.PopupEntity(Loc.GetString("bioseal-rollerbed-zipped"), entity);
}
