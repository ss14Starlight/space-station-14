using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.ActionBlocker;
using Content.Shared.Atmos.Rotting;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Kitchen.Components;
using Content.Shared.Light.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Stunnable;

namespace Content.Shared._Starlight.Actions.EntitySystems;

public sealed class SharedWrapSystem : EntitySystem
{

    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WrapActionEvent>(OnWrapAttempt);
        SubscribeLocalEvent<WrapDoAfterEvent>(OnWrap);
        SubscribeLocalEvent<WrappedComponent, InteractUsingEvent>(OnInteract);
        SubscribeLocalEvent<WrappedComponent, UnwrapDoAfterEvent>(OnUnwrap);
        SubscribeLocalEvent<WrappedComponent, StandUpAttemptEvent>(OnStandUpAttempt);
        SubscribeLocalEvent<WrappedComponent, IsRottingEvent>(OnRotting);
        SubscribeLocalEvent<WrappedComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
    }

    /// <summary>
    /// Prevent entity from standing up while wrapped.
    /// </summary>
    private void OnStandUpAttempt(Entity<WrappedComponent> ent, ref StandUpAttemptEvent args)
    {
        args.Cancelled = true;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent.Owner, ent.Comp.SelfUnWrapTime, new UnwrapDoAfterEvent(), ent.Owner, ent.Owner)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    /// <summary>
    /// Prevent entity from rotting while wrapped.
    /// </summary>
    private static void OnRotting(Entity<WrappedComponent> ent, ref IsRottingEvent args)
        => args.Handled = true;

    /// <summary>
    /// Prevent entity from moving while wrapped.
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="args"></param>
    private void OnUpdateCanMove(Entity<WrappedComponent> ent, ref UpdateCanMoveEvent args)
        => args.Cancel();

    /// <summary>
    /// Handle item interact for external unwrap.
    /// </summary>
    private void OnInteract(EntityUid uid, WrappedComponent component, InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<WrappedComponent>(args.Target) || !HasComp<SharpComponent>(args.Used))
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

    private void OnUnwrap(EntityUid uid, WrappedComponent component, UnwrapDoAfterEvent args)
    {
        if (args.Handled || !HasComp<WrappedComponent>(uid))
            return;
        args.Handled = true;
        RemComp<WrappedComponent>(uid);
        _blocker.UpdateCanMove(uid);
    }

    private void OnWrap(WrapDoAfterEvent args)
    {
        if (args.Handled || args.Args.EventTarget == null || HasComp<WrappedComponent>(args.Args.EventTarget) || !_stun.TryKnockdown(args.Args.EventTarget.Value, null, true, false, true, true))
            return;
        args.Handled = true;
        EnsureComp<WrappedComponent>(args.Args.EventTarget.Value);
        _blocker.UpdateCanMove(args.Args.EventTarget.Value);
    }

    private void OnWrapAttempt(WrapActionEvent args)
    {
        if (args.Handled || HasComp<WrappedComponent>(args.Target))
            return;

        args.Handled = true;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.Performer, args.WrapTime, new WrapDoAfterEvent(), args.Target, args.Target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
        });
    }
}
