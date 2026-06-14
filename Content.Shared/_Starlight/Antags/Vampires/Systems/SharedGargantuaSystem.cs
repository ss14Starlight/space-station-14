using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Antags.Vampires.Components.Classes;
using Content.Shared.Actions.Events;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Prying.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Antags.Vampires.Systems;

public sealed class SharedGargantuaSystem : EntitySystem
{
    private static readonly EntProtoId BloodSwellStatusEffect = "StatusEffectVampireBloodSwell";
    private static readonly EntProtoId BloodRushStatusEffect = "StatusEffectVampireBloodRush";

    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedVampireActionUseSystem _vampireActions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireBloodSwellActionEvent>(OnBloodSwell);
        SubscribeLocalEvent<VampireBloodRushActionEvent>(OnBloodRush);
        SubscribeLocalEvent<GargantuaComponent, VampireOverwhelmingForceActionEvent>(OnOverwhelmingForce);

        SubscribeLocalEvent<GargantuaComponent, PullAttemptEvent>(OnOverwhelmingForcePullAttempt);
        SubscribeLocalEvent<GargantuaComponent, DisarmAttemptEvent>(OnOverwhelmingForceDisarmAttempt);
        SubscribeLocalEvent<GargantuaComponent, AttemptMobTargetCollideEvent>(OnOverwhelmingForceMobPushAttempt);
        SubscribeLocalEvent<GargantuaComponent, UserBeforePryEvent>(OnOverwhelmingForceBeforePry);

        SubscribeLocalEvent<ActiveBloodSwellComponent, GetMeleeDamageEvent>(OnBloodSwellMeleeDamage);
        SubscribeLocalEvent<ActiveBloodSwellComponent, StatusEffectRelayedEvent<GetMeleeDamageEvent>>(OnBloodSwellMeleeDamage);
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeDamageEvent>(OnMeleeWeaponDamage);
        SubscribeLocalEvent<ActiveBloodRushComponent, StatusEffectRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<ActiveBloodSwellComponent, StatusEffectRemovedEvent>(OnBloodSwellRemoved);
        SubscribeLocalEvent<ActiveBloodRushComponent, StatusEffectRemovedEvent>(OnBloodRushRemoved);
    }

    private void OnBloodSwell(VampireBloodSwellActionEvent args)
    {
        if (args.Handled)
            return;

        var uid = args.Performer;
        var actionEntity = args.Action.Owner;
        if (!Exists(actionEntity)
            || !HasComp<GargantuaComponent>(uid)
            || !_vampireActions.TryUse(uid, actionEntity)
            || !_statusEffects.TrySetStatusEffectDuration(uid, BloodSwellStatusEffect, out var statusEffect, args.Duration))
        {
            return;
        }

        var active = EnsureComp<ActiveBloodSwellComponent>(statusEffect.Value);

        active.EnhancedThreshold = args.EnhancedThreshold;
        active.MeleeBonusDamage = FixedPoint2.New(args.MeleeBonusDamage);
        active.MeleeBonusDamageType = args.MeleeBonusDamageType;
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

        var damageType = ent.Comp.MeleeBonusDamageType;
        args.Damage.DamageDict.TryGetValue(damageType, out var damage);
        args.Damage.DamageDict[damageType] = damage + ent.Comp.MeleeBonusDamage;
    }

    private void OnBloodSwellMeleeDamage(Entity<ActiveBloodSwellComponent> ent, ref StatusEffectRelayedEvent<GetMeleeDamageEvent> args)
    {
        var ev = args.Args;
        OnBloodSwellMeleeDamage(ent, ref ev);
        args.Args = ev;
    }

    private void OnMeleeWeaponDamage(Entity<MeleeWeaponComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (args.Weapon == args.User)
            return;

        if (!_statusEffects.TryEffectsWithComp<ActiveBloodSwellComponent>(args.User, out var effects)
            || !TryComp<VampireComponent>(args.User, out var vampire))
        {
            return;
        }

        foreach (var effect in effects)
        {
            if (vampire.TotalBlood < effect.Comp1.EnhancedThreshold)
                continue;

            var damageType = effect.Comp1.MeleeBonusDamageType;
            args.Damage.DamageDict.TryGetValue(damageType, out var damage);
            args.Damage.DamageDict[damageType] = damage + effect.Comp1.MeleeBonusDamage;
        }
    }

    private void OnBloodRush(VampireBloodRushActionEvent args)
    {
        if (args.Handled)
            return;

        var uid = args.Performer;
        var actionEntity = args.Action.Owner;
        if (!Exists(actionEntity)
            || !HasComp<GargantuaComponent>(uid)
            || !_vampireActions.TryUse(uid, actionEntity)
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

    private void OnOverwhelmingForce(EntityUid uid, GargantuaComponent gargantua, ref VampireOverwhelmingForceActionEvent args)
    {
        if (args.Handled)
            return;

        var actionEntity = args.Action.Owner;
        if (!Exists(actionEntity)
            || !_vampireActions.TryUse(uid, actionEntity))
            return;

        gargantua.OverwhelmingForceActive = !gargantua.OverwhelmingForceActive;

        if (gargantua.OverwhelmingForceActive)
        {
            var prying = EnsureComp<PryingComponent>(uid);
            prying.PryPowered = true;
            prying.Force = true;
            prying.SpeedModifier = gargantua.OverwhelmingForcePrySpeedModifier;

            _popup.PopupPredicted(Loc.GetString("vampire-overwhelming-force-start"), uid, uid);
        }
        else
        {
            RemComp<PryingComponent>(uid);

            _popup.PopupPredicted(Loc.GetString("vampire-overwhelming-force-stop"), uid, uid);
        }

        if (_actions.GetAction(actionEntity) is { } action)
            _actions.SetToggled(action.AsNullable(), gargantua.OverwhelmingForceActive);

        Dirty(uid, gargantua);
        args.Handled = true;
    }

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
