using Content.Shared._Starlight.Changeling;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Stunnable;
using Content.Shared.Damage.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private EmagSystem _emag = default!;

    private static readonly EntProtoId ShockHandPrototype = "ChangelingShockHand";
    private static readonly EntProtoId PainSuppressionEffect = "ChangelingPainSuppression";

    private void InitializeAdaptations()
    {
        SubscribeLocalEvent<ChangelingComponent, ChangelingEmagEvent>(OnEmag);
        SubscribeLocalEvent<ChangelingComponent, ToggleChangelingRegenerationEvent>(OnToggleRegeneration);
        SubscribeLocalEvent<ChangelingComponent, ToggleChangelingPainSuppressionEvent>(OnTogglePainSuppression);
        SubscribeLocalEvent<ChangelingComponent, ToggleChangelingShockHandEvent>(OnToggleShockHand);
        SubscribeLocalEvent<ChangelingShockHandComponent, StaminaDamageOnHitAttemptEvent>(OnShockHandHit);
    }

    private void OnEmag(EntityUid uid, ChangelingComponent comp, ref ChangelingEmagEvent args)
    {
        if (args.Handled)
            return;

        // Use the ordinary emag prototype so its access flags and target restrictions stay identical.
        var tool = Spawn("EmagUnlimited", Transform(uid).Coordinates);
        var success = _emag.TryEmagEffect((tool, Comp<EmagComponent>(tool)), uid, args.Target);
        QueueDel(tool);
        if (!success)
            UpdateChemicals(uid, comp, Comp<ChangelingActionComponent>(args.Action).ChemicalCost);
        args.Handled = success;
    }

    private bool CanActivateAdaptation(EntityUid uid, ChangelingComponent comp)
    {
        if (comp.IsInStasis || _mobState.IsDead(uid))
            return false;
        if (comp.Biomass >= 1)
            return true;
        _popup.PopupEntity(Loc.GetString("changeling-biomass-deficit"), uid, uid);
        return false;
    }

    private void OnToggleRegeneration(EntityUid uid, ChangelingComponent comp, ref ToggleChangelingRegenerationEvent args)
    {
        if (args.Handled)
            return;
        if (comp.RegenerationAction != null)
            SetAdaptationAction(ref comp.RegenerationAction, null);
        else
        {
            if (!CanActivateAdaptation(uid, comp))
                return;
            var regeneration = Comp<ChangelingRegenerationActionComponent>(args.Action);
            if (comp.Chemicals < regeneration.ChemicalCostPerSecond * (float) comp.ChemicalUpdateCooldown.TotalSeconds)
            {
                _popup.PopupEntity(Loc.GetString("changeling-chemicals-deficit"), uid, uid);
                return;
            }
            SetAdaptationAction(ref comp.RegenerationAction, args.Action);
        }
        args.Handled = true;
    }

    private void OnTogglePainSuppression(EntityUid uid, ChangelingComponent comp, ref ToggleChangelingPainSuppressionEvent args)
    {
        if (args.Handled)
            return;
        if (comp.PainSuppressionAction != null)
            StopPainSuppression(uid, comp);
        else
        {
            if (!CanActivateAdaptation(uid, comp)
                || !_statusEffect.TryUpdateStatusEffectDuration(uid, PainSuppressionEffect, null))
                return;
            EnsureComp<ChangelingPainSuppressionComponent>(uid);
            SetAdaptationAction(ref comp.PainSuppressionAction, args.Action);
            _stamina.TakeStaminaDamage(uid, -_stamina.GetStaminaDamage(uid));
            _statusEffect.TryRemoveStatusEffect(uid, SharedStunSystem.StunId);
            _statusEffect.TryRemoveStatusEffect(uid, SharedStaminaSystem.StaminaLow);
            _speed.RefreshMovementSpeedModifiers(uid);
        }
        args.Handled = true;
    }

    private void OnToggleShockHand(EntityUid uid, ChangelingComponent comp, ref ToggleChangelingShockHandEvent args)
    {
        if (args.Handled)
            return;
        var active = comp.ShockHandAction != null;
        if (!active && !CanActivateAdaptation(uid, comp))
            return;
        if (!TryToggleItem(uid, ShockHandPrototype, comp))
            return;
        SetAdaptationAction(ref comp.ShockHandAction, active ? null : args.Action);
        args.Handled = true;
    }

    private void OnShockHandHit(Entity<ChangelingShockHandComponent> ent, ref StaminaDamageOnHitAttemptEvent args)
    {
        if (args.Cancelled)
            return;
        var user = Transform(ent).ParentUid;
        if (!TryComp<ChangelingComponent>(user, out var ling) || ling.Biomass < 1
            || ling.IsInStasis || ling.Chemicals < ent.Comp.ChemicalCost)
        {
            args.Cancelled = true;
            return;
        }
        UpdateChemicals(user, ling, -ent.Comp.ChemicalCost);
    }

    private void UpdateAdaptations(EntityUid uid, ChangelingComponent comp)
    {
        if (comp.Biomass < 1 || comp.IsInStasis || _mobState.IsDead(uid))
        {
            StopAdaptations(uid, comp);
            return;
        }
        if (comp.PainSuppressionAction != null && !_statusEffect.HasStatusEffect(uid, PainSuppressionEffect))
            StopPainSuppression(uid, comp);
        if (comp.ShockHandAction != null
            && (!comp.Equipment.TryGetValue(ShockHandPrototype, out var item) || item == null
                || TerminatingOrDeleted(item.Value) || !_hands.IsHolding(uid, item.Value)))
            StopShockHand(comp);

        if (comp.RegenerationAction is not { } action)
            return;
        if (!TryComp<ChangelingRegenerationActionComponent>(action, out var regeneration)
            || comp.Chemicals < regeneration.ChemicalCostPerSecond * (float) comp.ChemicalUpdateCooldown.TotalSeconds)
        {
            SetAdaptationAction(ref comp.RegenerationAction, null);
            _popup.PopupEntity(Loc.GetString("changeling-passive-disable"), uid, uid);
            return;
        }

        var seconds = (float) comp.ChemicalUpdateCooldown.TotalSeconds;
        UpdateChemicals(uid, comp, -regeneration.ChemicalCostPerSecond * seconds);
        if (!HasComp<DamageableComponent>(uid))
            return;
        var healing = new DamageSpecifier();
        foreach (var (type, damage) in _damage.GetAllDamage(uid).DamageDict)
        {
            if (damage > 0)
                healing.DamageDict[type] = -FixedPoint2.Min(damage, FixedPoint2.New(regeneration.HealingPerSecond * seconds));
        }
        _damage.TryChangeDamage(uid, healing, ignoreResistances: true, interruptsDoAfters: false);
    }

    private void SetAdaptationAction(ref EntityUid? current, EntityUid? next)
    {
        if (current != null)
            _actions.SetToggled(current, false);
        current = next;
        if (next != null)
            _actions.SetToggled(next, true);
    }

    private void StopPainSuppression(EntityUid uid, ChangelingComponent comp)
    {
        if (comp.PainSuppressionAction == null)
            return;
        SetAdaptationAction(ref comp.PainSuppressionAction, null);
        RemComp<ChangelingPainSuppressionComponent>(uid);
        _statusEffect.TryRemoveStatusEffect(uid, PainSuppressionEffect);
        _speed.RefreshMovementSpeedModifiers(uid);
    }

    private void StopShockHand(ChangelingComponent comp)
    {
        if (comp.ShockHandAction == null)
            return;
        SetAdaptationAction(ref comp.ShockHandAction, null);
        if (comp.Equipment.Remove(ShockHandPrototype, out var item))
            QueueDel(item);
    }

    private void StopAdaptations(EntityUid uid, ChangelingComponent comp)
    {
        SetAdaptationAction(ref comp.RegenerationAction, null);
        StopPainSuppression(uid, comp);
        StopShockHand(comp);
    }
}
