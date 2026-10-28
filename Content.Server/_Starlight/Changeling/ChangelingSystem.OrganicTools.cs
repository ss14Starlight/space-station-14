using Content.Shared._Starlight.Changeling;
using Content.Shared.Damage.Events;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private EmagSystem _organicEmag = default!;

    private static readonly EntProtoId ShockHandPrototype = "ChangelingShockHand";

    [SubscribeLocalEvent]
    private void OnOrganicEmag(Entity<ChangelingComponent> ent, ref ChangelingEmagEvent args)
    {
        if (args.Handled || ent.Comp.IsInStasis || _mobState.IsDead(ent))
            return;

        if (ent.Comp.Biomass < 1)
        {
            _popup.PopupEntity(Loc.GetString("changeling-biomass-deficit"), ent, ent);
            return;
        }
        if (ent.Comp.IsInLesserForm)
        {
            _popup.PopupEntity(Loc.GetString("changeling-action-fail-lesserform"), ent, ent);
            return;
        }

        if (ent.Comp.Chemicals < args.ChemicalCost)
        {
            _popup.PopupEntity(Loc.GetString("changeling-chemicals-deficit"), ent, ent);
            return;
        }

        // Reuse the access breaker's target rules and charge only a successful hack.
        var tool = Spawn("AccessBreakerUnlimited", Transform(ent).Coordinates);
        args.Handled = _organicEmag.TryEmagEffect((tool, Comp<EmagComponent>(tool)), ent, args.Target);
        QueueDel(tool);
        if (args.Handled)
            UpdateChemicals(ent, ent.Comp, -args.ChemicalCost);
    }

    [SubscribeLocalEvent]
    private void OnToggleOrganicShockHand(Entity<ChangelingComponent> ent, ref ToggleChangelingShockHandEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.IsInLesserForm)
        {
            _popup.PopupEntity(Loc.GetString("changeling-action-fail-lesserform"), ent, ent);
            return;
        }

        var active = ent.Comp.Equipment.ContainsKey(ShockHandPrototype.Id);
        if (!active && (ent.Comp.IsInStasis || _mobState.IsDead(ent) || ent.Comp.Biomass < 1))
            return;
        if (!TryToggleItem(ent, ShockHandPrototype, ent.Comp))
            return;

        if (!active && ent.Comp.Equipment[ShockHandPrototype.Id] is { } item)
        {
            var hand = Comp<ChangelingShockHandComponent>(item);
            hand.User = ent;
            hand.Action = args.Action.Owner;
        }
        _actions.SetToggled(args.Action.AsNullable(), !active);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnOrganicShockAttempt(Entity<ChangelingShockHandComponent> ent, ref StaminaDamageOnHitAttemptEvent args)
    {
        if (!TryGetOrganicShockUser(ent, out _))
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnOrganicShockHit(Entity<ChangelingShockHandComponent> ent, ref StaminaMeleeHitEvent args)
    {
        if (!args.Handled && args.HitList.Count > 0 && TryGetOrganicShockUser(ent, out var user))
            UpdateChemicals(user, user.Comp, -ent.Comp.ChemicalCost);
    }

    private bool TryGetOrganicShockUser(Entity<ChangelingShockHandComponent> ent, out Entity<ChangelingComponent> user)
    {
        user = default;
        if (ent.Comp.User is not { } owner || !TryComp<ChangelingComponent>(owner, out var ling)
            || !_hands.IsHolding(owner, ent.Owner) || ling.IsInStasis || _mobState.IsDead(owner)
            || ling.Biomass < 1 || ling.Chemicals < ent.Comp.ChemicalCost)
            return false;

        user = (owner, ling);
        return true;
    }

    [SubscribeLocalEvent]
    private void OnOrganicShockRemoved(Entity<ChangelingShockHandComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Action is { } action && !TerminatingOrDeleted(action))
            _actions.SetToggled(action, false);
        if (ent.Comp.User is { } owner && TryComp<ChangelingComponent>(owner, out var ling)
            && ling.Equipment.TryGetValue(ShockHandPrototype.Id, out var item) && item == ent.Owner)
            ling.Equipment.Remove(ShockHandPrototype.Id);
    }

    [SubscribeLocalEvent]
    private void OnOrganicShockExamine(Entity<ChangelingComponent> ent, ref ExaminedEvent args)
    {
        foreach (var held in _hands.EnumerateHeld(ent.Owner))
        {
            if (!HasComp<ChangelingShockHandComponent>(held))
                continue;
            args.PushText(Loc.GetString("changeling-shock-hand-examine"));
            break;
        }
    }
}
