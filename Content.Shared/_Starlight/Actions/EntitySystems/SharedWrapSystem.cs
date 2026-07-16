using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.ActionBlocker;
using Content.Shared.Atmos.Rotting;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Kitchen.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Humanoid;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Content.Shared.Alert;
using Content.Shared.Hands.EntitySystems;

namespace Content.Shared._Starlight.Actions.EntitySystems;

public sealed partial class SharedWrapSystem : EntitySystem
{

    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WrapActionEvent>(OnWrapAttempt);
        SubscribeLocalEvent<HumanoidAppearanceComponent, WrapDoAfterEvent>(OnWrap);
        SubscribeLocalEvent<WrappedComponent, UnWrapAlertEvent>(OnAlertUnwrap);
        SubscribeLocalEvent<WrapEntityHolderComponent, InteractUsingEvent>(OnInteract);
        SubscribeLocalEvent<WrapEntityHolderComponent, InteractHandEvent>(OnHandInteract);
        SubscribeLocalEvent<WrapEntityHolderComponent, UnwrapDoAfterEvent>(OnUnwrap);
        SubscribeLocalEvent<WrapEntityHolderComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WrappedComponent, IsRottingEvent>(OnRotting);
        SubscribeLocalEvent<WrappedComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
    }

    /// <summary>
    /// Prevent entity from rotting while wrapped.
    /// </summary>
    private static void OnRotting(Entity<WrappedComponent> ent, ref IsRottingEvent args)
        => args.Handled = true;

    /// <summary>
    /// Prevent entity from moving while wrapped.
    /// </summary>
    private void OnUpdateCanMove(Entity<WrappedComponent> ent, ref UpdateCanMoveEvent args)
        => args.Cancel();

    /// <summary>
    /// Handle item interact for external unwrap.
    /// </summary>
    private void OnInteract(EntityUid uid, WrapEntityHolderComponent component, InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<SharpComponent>(args.Used))
            return;

        args.Handled = true;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, component.UnWrapItemTime, new UnwrapDoAfterEvent(), args.Target, args.Target, args.Used)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
        });
    }

    private void OnHandInteract(EntityUid uid, WrapEntityHolderComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, component.UnWrapHandTime, new UnwrapDoAfterEvent(), args.Target, args.Target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
        });
    }

    private void OnAlertUnwrap(EntityUid uid, WrappedComponent component, UnWrapAlertEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (component.Holder == null || Deleted(component.Holder) || !TryComp<WrapEntityHolderComponent>(component.Holder.Value, out var holderComp))
        {
            RemComp<WrappedComponent>(uid);
            _alerts.ClearAlert(uid, args.AlertId);
            return;
        }

        var activeItem = _handsSystem.GetActiveItem(uid);
        TimeSpan time = activeItem == null ? holderComp.UnWrapHandTime : HasComp<SharpComponent>(activeItem) ? holderComp.UnWrapItemTime : holderComp.UnWrapHandTime;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, uid, time, new UnwrapDoAfterEvent(), component.Holder.Value, component.Holder.Value)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
        });
    }

    private void OnStartup(EntityUid uid, WrapEntityHolderComponent component, ComponentStartup args)
        => component.Container = _container.EnsureContainer<Container>(uid, component.ContainerId);

    private void OnUnwrap(EntityUid uid, WrapEntityHolderComponent component, UnwrapDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        if (component.Hold is { } held && !Deleted(held))
        {
            if (_container.TryGetContainingContainer(uid, held, out var container))
                _container.Remove(held, container, true, true);
            RemComp<WrappedComponent>(held);
            _blocker.UpdateCanMove(held);
            _alerts.ClearAlert(held, component.WrappedAlert);
            component.Hold = null;
            PredictedQueueDel(uid);
        }
    }

    private void OnWrap(EntityUid uid, HumanoidAppearanceComponent _, WrapDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !_gameTiming.IsFirstTimePredicted || HasComp<WrappedComponent>(uid))
            return;
        var wrapped = EnsureComp<WrappedComponent>(uid);
        _blocker.UpdateCanMove(uid);
        var xform = Transform(uid);
        var holder = PredictedSpawnAttachedTo(args.WrapContainerId, xform.Coordinates);
        wrapped.Holder = holder;

        if (_net.IsServer && TryComp<WrapEntityHolderComponent>(holder, out var holderComp)) // Server-only: client-side container insertion causes metadata errors during prediction.
        {
            if (holderComp.Container == null || !_container.Insert(uid, holderComp.Container))
            {
                RemComp<WrappedComponent>(uid);
                _blocker.UpdateCanMove(uid);
                PredictedQueueDel(holder);
                return;
            }

            _alerts.ShowAlert(uid, holderComp.WrappedAlert);

            holderComp.Hold = uid;

            DirtyField(holder, holderComp, nameof(WrapEntityHolderComponent.Hold));
        }
        args.Handled = true;
    }

    private void OnWrapAttempt(WrapActionEvent args)
    {
        if (args.Handled || HasComp<WrappedComponent>(args.Target))
            return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.Performer, args.WrapTime, new WrapDoAfterEvent(args.WrapContainerId), args.Target, args.Target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
        });

        args.Handled = true;
    }
}
