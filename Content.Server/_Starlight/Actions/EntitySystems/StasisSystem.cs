using Content.Server.Body.Systems;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Robust.Shared.Timing;
using Content.Shared.Mobs;
using Content.Shared._Starlight.Actions.EntitySystems;
using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Player;
using Content.Shared.Damage.Systems;

namespace Content.Server._Starlight.Actions.EntitySystems;

public sealed class StasisSystem : SharedStasisSystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StasisComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<StasisComponent, ComponentShutdown>(OnCompRemove);
        SubscribeLocalEvent<StasisComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<StasisComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<StasisComponent, PrepareStasisActionEvent>(OnPrepareStasisStart);
        SubscribeLocalEvent<StasisComponent, EnterStasisActionEvent>(OnEnterStasisStart);
        SubscribeLocalEvent<StasisComponent, ExitStasisActionEvent>(OnExitStasisStart);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        
        var query = EntityQueryEnumerator<StasisComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextHeal > curTime || !comp.IsInStasis)
                continue;

            // Negative because your healing
            var modifier = _mobState.IsCritical(uid) ? -comp.CritHealingModifier : -1.0f;

            _damageable.TryChangeDamage(uid, modifier * comp.HealingPerUpdate, true, origin: uid);

            _bloodstream.TryModifyBleedAmount(uid, modifier * comp.BleedHealPerUpdate);

            comp.NextHeal += comp.UpdateInterval;
        }
    }

    private void OnMapInit(EntityUid uid, StasisComponent comp, MapInitEvent args)
    {
        _actionsSystem.AddAction(uid, ref comp.EnterStasisActionEntity, comp.EnterStasisAction);
    }

    private void OnCompRemove(EntityUid uid, StasisComponent comp, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, comp.EnterStasisActionEntity);
        _actionsSystem.RemoveAction(uid, comp.ExitStasisActionEntity);
    }

    private void OnMobStateChanged(Entity<StasisComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead && ent.Comp.IsInStasis)
            RaiseLocalEvent(args.Target, new ExitStasisActionEvent());
    }

    private void OnDamageModify(Entity<StasisComponent> ent, ref DamageModifyEvent args)
    {
        // TODO: this might mean like hitting yourself with a bomb or something while in stasis wont resist damage.
        if (!ent.Comp.IsInStasis || args.Origin == ent)
            return;
        
        // Reduce all positive damage.
        var updatedDamage = new DamageSpecifier();
        foreach (var damage in args.Damage.DamageDict)
        {
            var scaler = ent.Comp.StasisDamageReduction;
            updatedDamage.DamageDict[damage.Key] = damage.Value > 0 ? scaler * damage.Value : damage.Value;
        }

        args.Damage = updatedDamage;
    }

    private void OnPrepareStasisStart(EntityUid uid, StasisComponent comp, PrepareStasisActionEvent args)
    {
        EnsureComp<StasisFrozenComponent>(uid);

        _actionsSystem.RemoveAction(uid, comp.EnterStasisActionEntity);
        _actionsSystem.AddAction(uid, ref comp.ExitStasisActionEntity, comp.ExitStasisAction);
        _actionsSystem.SetCooldown(comp.ExitStasisActionEntity, comp.StasisEnterEffectLifetime);

        // Send animation event to all clients
        var ev = new StasisAnimationEvent(GetNetEntity(uid), GetNetCoordinates(Transform(uid).Coordinates), StasisAnimationType.Prepare);
        RaiseNetworkEvent(ev, Filter.Pvs(uid, entityManager: EntityManager));

        // TODO: refactor this to not use timers
        // Schedule the enter stasis event after delay
        Timer.Spawn(comp.StasisEnterEffectLifetime, () =>
        {
            if (!HasComp<StasisComponent>(uid))
                return;

            var enterEv = new EnterStasisActionEvent();
            RaiseLocalEvent(uid, enterEv);
        });
    }

    private void OnEnterStasisStart(EntityUid uid, StasisComponent comp, EnterStasisActionEvent args)
    {
        comp.IsInStasis = true;
        comp.NextHeal = _timing.CurTime;
        comp.IsVisible = false; // Entity becomes invisible when entering stasis to better show the effect

        Dirty(uid, comp);

        // Send animation event to all clients
        var ev = new StasisAnimationEvent(GetNetEntity(uid), GetNetCoordinates(Transform(uid).Coordinates), StasisAnimationType.Enter);
        RaiseNetworkEvent(ev, Filter.Pvs(uid, entityManager: EntityManager));
    }

    private void OnExitStasisStart(EntityUid uid, StasisComponent comp, ExitStasisActionEvent args)
    {
        comp.IsInStasis = false;
        comp.IsVisible = true; // Entity becomes visible when exiting stasis

        Dirty(uid, comp);

        _actionsSystem.RemoveAction(uid, comp.ExitStasisActionEntity);
        _actionsSystem.AddAction(uid, ref comp.EnterStasisActionEntity, comp.EnterStasisAction);
        _actionsSystem.SetCooldown(comp.EnterStasisActionEntity, comp.StasisCooldown);

        RemComp<StasisFrozenComponent>(uid);

        // Send animation event to all clients
        var ev = new StasisAnimationEvent(GetNetEntity(uid), GetNetCoordinates(Transform(uid).Coordinates),
            StasisAnimationType.Exit);
        RaiseNetworkEvent(ev, Filter.Pvs(uid, entityManager: EntityManager));
    }
}
