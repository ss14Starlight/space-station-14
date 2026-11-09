using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.FireDragon;

/// <summary>
/// Predicts Roar and shares ability recovery between the client and server.
/// </summary>
public abstract partial class SharedFireDragonSystem : EntitySystem
{
    [Dependency] protected SharedActionsSystem Actions = default!;
    [Dependency] protected SharedTransformSystem TransformSystem = default!;
    [Dependency] protected MobStateSystem MobState = default!;
    [Dependency] protected ActionBlockerSystem Blocker = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected OccluderSystem Occluder = default!;
    [Dependency] protected NpcFactionSystem Factions = default!;
    [Dependency] private MovementModStatusSystem _movementStatus = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    [SubscribeLocalEvent]
    private void OnRoar(Entity<FireDragonComponent> ent, ref FireDragonRoarEvent args)
    {
        if (!CanUse(ent, args))
            return;

        FinishAbility(ent, args);
        if (!Timing.IsFirstTimePredicted)
            return;

        var origin = TransformSystem.GetMapCoordinates(ent);
        foreach (var target in _lookup.GetEntitiesInRange(origin, ent.Comp.RoarRange))
        {
            if (!HasComp<MovementSpeedModifierComponent>(target)
                || !MobState.IsAlive(target)
                || Factions.IsEntityFriendly(ent.Owner, target)
                || !Occluder.InRangeUnoccluded(origin, TransformSystem.GetMapCoordinates(target), 0f, ignoreTouching: true))
                continue;

            _movementStatus.TryUpdateMovementSpeedModDuration(target, ent.Comp.HuntEffect,
                ent.Comp.RoarDuration, ent.Comp.RoarSpeedModifier);
        }
    }

    protected bool CanUse(Entity<FireDragonComponent> ent, BaseActionEvent args) =>
        !args.Handled
        && !Actions.IsCooldownActive(args.Action.Comp)
        && IsReadyForAbility(ent);

    protected bool IsReadyForAbility(Entity<FireDragonComponent> ent) =>
        Timing.CurTime >= ent.Comp.NextAbility
        && MobState.IsAlive(ent)
        && Blocker.CanAttack(ent)
        && !HasComp<FireDragonDashComponent>(ent);

    protected void FinishAbility(Entity<FireDragonComponent> ent, BaseActionEvent args)
    {
        args.Handled = true;
        var recovery = ent.Comp.AbilityRecovery;
        var usedAction = args.Action.Owner;
        ent.Comp.NextAbility = Timing.CurTime + recovery;
        Dirty(ent);

        SetRecoveryCooldown(ent.Comp.AncientFlameActionEntity, usedAction, recovery);
        SetRecoveryCooldown(ent.Comp.TailSlamActionEntity, usedAction, recovery);
        SetRecoveryCooldown(ent.Comp.RoarActionEntity, usedAction, recovery);
        SetRecoveryCooldown(ent.Comp.WingDashActionEntity, usedAction, recovery);
        SetRecoveryCooldown(ent.Comp.FirebreathActionEntity, usedAction, recovery);
    }

    private void SetRecoveryCooldown(EntityUid? actionUid, EntityUid usedAction, TimeSpan recovery)
    {
        // Actions applies the used action's full cooldown after its event returns.
        if (actionUid is { } uid && uid != usedAction)
            Actions.SetIfBiggerCooldown(uid, recovery);
    }
}
