using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Antags.Vampires.Components.Classes;
using Content.Shared.Actions.Events;
using Content.Shared.Alert;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Prying.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Antags.Vampires.Systems;

public abstract class SharedGargantuaSystem : EntitySystem
{
    private static readonly EntProtoId BloodSwellStatusEffect = "StatusEffectVampireBloodSwell";
    private static readonly EntProtoId BloodRushStatusEffect = "StatusEffectVampireBloodRush";

    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireBloodSwellActionEvent>(OnBloodSwell);
        SubscribeLocalEvent<VampireBloodRushActionEvent>(OnBloodRush);

        SubscribeLocalEvent<GargantuaComponent, PullAttemptEvent>(OnOverwhelmingForcePullAttempt);
        SubscribeLocalEvent<GargantuaComponent, DisarmAttemptEvent>(OnOverwhelmingForceDisarmAttempt);
        SubscribeLocalEvent<GargantuaComponent, AttemptMobTargetCollideEvent>(OnOverwhelmingForceMobPushAttempt);
        SubscribeLocalEvent<GargantuaComponent, UserBeforePryEvent>(OnOverwhelmingForceBeforePry);

        SubscribeLocalEvent<ActiveBloodSwellComponent, GetMeleeDamageEvent>(OnBloodSwellMeleeDamage);
        SubscribeLocalEvent<ActiveBloodSwellComponent, StatusEffectRelayedEvent<GetMeleeDamageEvent>>(OnBloodSwellMeleeDamage);
        SubscribeLocalEvent<ActiveBloodRushComponent, StatusEffectRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<ActiveBloodSwellComponent, StatusEffectRemovedEvent>(OnBloodSwellRemoved);
        SubscribeLocalEvent<ActiveBloodRushComponent, StatusEffectRemovedEvent>(OnBloodRushRemoved);
    }

    protected virtual bool TryUseVampireAction(EntityUid uid, EntityUid actionEntity)
        => true;

    private void OnBloodSwell(VampireBloodSwellActionEvent args)
    {
        if (args.Handled)
            return;

        var uid = args.Performer;
        var actionEntity = args.Action.Owner;
        if (!Exists(actionEntity)
            || !HasComp<GargantuaComponent>(uid)
            || !TryUseVampireAction(uid, actionEntity)
            || !_statusEffects.TrySetStatusEffectDuration(uid, BloodSwellStatusEffect, out var statusEffect, args.Duration))
        {
            return;
        }

        var active = EnsureComp<ActiveBloodSwellComponent>(statusEffect.Value);

        active.EnhancedThreshold = args.EnhancedThreshold;
        active.MeleeBonusDamage = FixedPoint2.New(args.MeleeBonusDamage);
        active.ReducedDamageTypes.Clear();
        foreach (var damageType in args.ReducedDamageTypes)
        {
            active.ReducedDamageTypes.Add(damageType);
        }
        active.IncomingDamageMultiplier = args.IncomingDamageMultiplier;
        active.StaminaDamageMultiplier = args.StaminaDamageMultiplier;
        active.StatusEffectDurationMultiplier = args.StatusEffectDurationMultiplier;

        Dirty(statusEffect.Value, active);
        _popup.PopupPredicted(Loc.GetString("vampire-blood-swell-start"), uid, uid);
        args.Handled = true;
    }

    private void OnBloodSwellMeleeDamage(Entity<ActiveBloodSwellComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (!TryComp<StatusEffectComponent>(ent, out var effect)
            || effect.AppliedTo is not { } target
            || !TryComp<VampireComponent>(target, out var vampire))
            return;

        if (args.Weapon != target)
            return;

        if (vampire.TotalBlood < ent.Comp.EnhancedThreshold)
            return;

        args.Damage.DamageDict.TryGetValue("Blunt", out var blunt);
        args.Damage.DamageDict["Blunt"] = blunt + ent.Comp.MeleeBonusDamage;
    }

    private void OnBloodSwellMeleeDamage(Entity<ActiveBloodSwellComponent> ent, ref StatusEffectRelayedEvent<GetMeleeDamageEvent> args)
    {
        var ev = args.Args;
        OnBloodSwellMeleeDamage(ent, ref ev);
        args.Args = ev;
    }

    private void OnBloodRush(VampireBloodRushActionEvent args)
    {
        if (args.Handled)
            return;

        var uid = args.Performer;
        var actionEntity = args.Action.Owner;
        if (!Exists(actionEntity)
            || !HasComp<GargantuaComponent>(uid)
            || !TryUseVampireAction(uid, actionEntity)
            || !_statusEffects.TrySetStatusEffectDuration(uid, BloodRushStatusEffect, out var statusEffect, args.Duration))
        {
            return;
        }

        var active = EnsureComp<ActiveBloodRushComponent>(statusEffect.Value);

        active.SpeedMultiplier = args.SpeedMultiplier;

        _movement.RefreshMovementSpeedModifiers(uid);
        Dirty(statusEffect.Value, active);
        _popup.PopupPredicted(Loc.GetString("vampire-blood-rush-start"), uid, uid);
        args.Handled = true;
    }

    private void OnBloodSwellRemoved(Entity<ActiveBloodSwellComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _popup.PopupPredicted(Loc.GetString("vampire-blood-swell-end"), args.Target, args.Target);
    }

    private void OnBloodRushRemoved(Entity<ActiveBloodRushComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(args.Target);
        _popup.PopupPredicted(Loc.GetString("vampire-blood-rush-end"), args.Target, args.Target);
    }

    private void OnRefreshMovementSpeed(Entity<ActiveBloodRushComponent> ent, ref StatusEffectRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
        => args.Args.ModifySpeed(ent.Comp.SpeedMultiplier, ent.Comp.SpeedMultiplier);

    private void OnOverwhelmingForcePullAttempt(EntityUid uid, GargantuaComponent component, PullAttemptEvent args)
    {
        if (!component.OverwhelmingForceActive || args.PulledUid != uid)
            return;

        args.Cancelled = true;
        _popup.PopupPredicted(Loc.GetString("vampire-overwhelming-force-too-heavy"), uid, args.PullerUid, PopupType.MediumCaution);
    }

    private void OnOverwhelmingForceDisarmAttempt(EntityUid uid, GargantuaComponent component, ref DisarmAttemptEvent args)
    {
        if (!component.OverwhelmingForceActive || args.TargetUid != uid)
            return;

        args.Cancelled = true;
        _popup.PopupPredicted(Loc.GetString("vampire-overwhelming-force-too-heavy"), uid, args.DisarmerUid, PopupType.MediumCaution);
    }

    private void OnOverwhelmingForceMobPushAttempt(EntityUid uid, GargantuaComponent component, ref AttemptMobTargetCollideEvent args)
    {
        if (!component.OverwhelmingForceActive)
            return;

        args.Cancelled = true;
    }

    private void OnOverwhelmingForceBeforePry(EntityUid uid, GargantuaComponent component, ref UserBeforePryEvent args)
    {
        if (!component.OverwhelmingForceActive
            || !TryComp<VampireComponent>(uid, out var vampire)
            || vampire.TotalBlood >= component.OverwhelmingForceDoorPryBloodCost)
        {
            return;
        }

        args.Cancelled = true;
        args.Message = "vampire-not-enough-blood";
    }
}
