using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Pulling.Events;
using Content.Shared.Speech;
using Content.Shared.Standing;
using Content.Shared.Strip.Components;
using Content.Shared.Throwing;

// ReSharper disable  CheckNamespace
namespace Content.Shared.Mobs.Systems;

public partial class MobStateSystem : EntitySystem
{
    /// <summary>
    ///  Check if a Mob is in Soft Critical
    /// </summary>
    /// <param name="target">Target Entity</param>
    /// <param name="component">The MobState component owned by the target</param>
    /// <returns>If the entity is Critical</returns>
    public bool IsSoftCritical(EntityUid target, MobStateComponent? component = null)
    {
        if (!_mobStateQuery.Resolve(target, ref component, false))
            return false;
        return component.CurrentState == MobState.SoftCritical;
    }

    [SubscribeLocalEvent]
    private void OnRefreshMovementSpeed(Entity<MobStateComponent> ent,
        ref RefreshMovementSpeedModifiersEvent args)
    {
        if(ent.Comp.CurrentState !=  MobState.SoftCritical)
            return;
        args.ModifySpeed(0.3f, 0.3f);
    }

    private bool SLOnStateExitSubscribers(EntityUid target, MobStateComponent component, MobState state)
    {
        if (state != MobState.SoftCritical) return false;
        _standing.Stand(target);
        return true;
    }

    private bool SLStateEnteredSubscribers(EntityUid target, MobStateComponent component, MobState state)
    {
        if (state != MobState.SoftCritical) return false;
        Down(target);
        _appearance.SetData(target, MobStateVisuals.State, MobState.Critical);
        return true;
    }

    private void SLOnGettingStripped(EntityUid target, MobStateComponent component, BeforeGettingStrippedEvent args)
    {
        if (IsSoftCritical(target, component))
            args.Multiplier /= 2;
    }

    private bool SLCheckAct(EntityUid target, MobStateComponent component, CancellableEntityEventArgs args)
    {
        if (!IsSoftCritical(target, component))
            return false;

        switch (args)
        {
            case AttackAttemptEvent:
            case ThrowAttemptEvent:
            case StandAttemptEvent:
            case StartPullAttemptEvent:
                args.Cancel();
                break;
        }
        return true;
    }


}
