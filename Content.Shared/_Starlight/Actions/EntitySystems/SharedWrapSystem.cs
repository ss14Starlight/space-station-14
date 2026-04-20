using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.ActionBlocker;
using Content.Shared.Atmos.Rotting;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Kitchen.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Stunnable;
using Content.Shared.Humanoid;
using Robust.Shared.Containers;

namespace Content.Shared._Starlight.Actions.EntitySystems;

public sealed class SharedWrapSystem : EntitySystem
{

    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _xformSys = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WrapActionEvent>(OnWrapAttempt);
        SubscribeLocalEvent<HumanoidAppearanceComponent, WrapDoAfterEvent>(OnWrap);
        SubscribeLocalEvent<WrapEntityHolderComponent, InteractUsingEvent>(OnInteract);
        SubscribeLocalEvent<WrapEntityHolderComponent, UnwrapDoAfterEvent>(OnUnwrap);
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

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, component.UnWrapTime, new UnwrapDoAfterEvent(), args.Target, args.Target, args.Used)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
        });
    }

    private void OnUnwrap(EntityUid uid, WrapEntityHolderComponent component, UnwrapDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (component.Hold != null && _container.TryGetContainingContainer(uid, component.Hold.Value, out var container))
        {
            _container.Remove(component.Hold.Value, container, true, true);
            RemComp<WrappedComponent>(component.Hold.Value);
            _blocker.UpdateCanMove(component.Hold.Value);
            component.Hold = null;
        }

        if (component.Hold == null)
            PredictedQueueDel(uid);
    }

    private void OnWrap(EntityUid uid, HumanoidAppearanceComponent _, WrapDoAfterEvent args)
    {
        if (args.Handled || HasComp<WrappedComponent>(uid))
            return;
        args.Handled = true;
        var wrapped = EnsureComp<WrappedComponent>(uid);
        _blocker.UpdateCanMove(uid);
        var xform = Transform(uid);
        var holder = PredictedSpawnAttachedTo(args.WrapContainerId, xform.Coordinates);

        EnsureComp<WrapEntityHolderComponent>(holder, out var holderComp);

        if (_container.TryGetContainer(holder, holderComp.ContainerId, out var container))
        {
            _container.Insert(uid, container, force: true);
            holderComp.Hold = uid;
        }
    }

    private void OnWrapAttempt(WrapActionEvent args)
    {
        if (args.Handled || HasComp<WrappedComponent>(args.Target))
            return;

        args.Handled = true;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.Performer, args.WrapTime, new WrapDoAfterEvent(args.WrapContainerId), args.Target, args.Target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
        });
    }
}
