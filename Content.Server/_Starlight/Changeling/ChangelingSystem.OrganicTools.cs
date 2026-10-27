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

    /// <summary>
    /// Applies the standard access breaker's effects and immunity rules through a temporary tool.
    /// Marks the action handled only when the target accepts the hack, so failed attempts do not spend chemicals.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnOrganicEmag(Entity<ChangelingComponent> ent, ref ChangelingEmagEvent args)
    {
        if (args.Handled || ent.Comp.IsInStasis || _mobState.IsDead(ent)
            || !Transform(ent).Coordinates.TryDistance(EntityManager, Transform(args.Target).Coordinates, out var distance)
            || distance > 1.5f
            || !TryComp<ChangelingActionComponent>(args.Action, out var action)
            || ent.Comp.Chemicals < action.ChemicalCost)
            return;

        // The prototype supplies both the Access emag type and AccessBreakerImmune tag.
        var tool = Spawn("AccessBreakerUnlimited", Transform(ent).Coordinates);
        args.Handled = _organicEmag.TryEmagEffect((tool, Comp<EmagComponent>(tool)), ent, args.Target);
        QueueDel(tool);
    }

    /// <summary>
    /// Grows or retracts a shock hand and records the owner and action used by hit validation and cleanup.
    /// Retraction remains available even when the changeling can no longer grow or power the hand.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnToggleOrganicShockHand(Entity<ChangelingComponent> ent, ref ToggleChangelingShockHandEvent args)
    {
        if (args.Handled)
            return;

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

    /// <summary>
    /// Prevents the hand's stamina effect when its original owner cannot power it or is no longer holding it.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnOrganicShockAttempt(Entity<ChangelingShockHandComponent> ent, ref StaminaDamageOnHitAttemptEvent args)
    {
        if (!TryGetOrganicShockUser(ent, out _))
            args.Cancelled = true;
    }

    /// <summary>
    /// Charges chemicals once for an eligible melee-hit event with at least one target, not once per target.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnOrganicShockHit(Entity<ChangelingShockHandComponent> ent, ref StaminaMeleeHitEvent args)
    {
        if (!args.Handled && args.HitList.Count > 0 && TryGetOrganicShockUser(ent, out var user))
            UpdateChemicals(user, user.Comp, -ent.Comp.ChemicalCost);
    }

    /// <summary>
    /// Resolves the hand's original owner and checks possession, stasis, death, biomass and chemical reserves.
    /// Both the stamina effect and its chemical charge use this eligibility check.
    /// </summary>
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

    /// <summary>
    /// Clears the action toggle and this hand's equipment entry when the hand is retracted or otherwise removed.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnOrganicShockRemoved(Entity<ChangelingShockHandComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Action is { } action && !TerminatingOrDeleted(action))
            _actions.SetToggled(action, false);
        if (ent.Comp.User is { } owner && TryComp<ChangelingComponent>(owner, out var ling)
            && ling.Equipment.TryGetValue(ShockHandPrototype.Id, out var item) && item == ent.Owner)
            ling.Equipment.Remove(ShockHandPrototype.Id);
    }

    /// <summary>
    /// Adds the visible glow cue whenever a shock hand is held, even if its owner currently cannot power a strike.
    /// </summary>
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
