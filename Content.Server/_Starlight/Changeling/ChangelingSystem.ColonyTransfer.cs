using Content.Shared._Starlight.Changeling;
using Content.Shared.Mind;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    private bool TransferColony(EntityUid mind, ChangelingColonyComponent colony, EntityUid target)
    {
        if (colony.Moving || !colony.Minds.ContainsKey(mind) || !CanEnterHiveCarrier(target)
            || !TryComp<MindComponent>(mind, out var consciousness) || consciousness.OwnedEntity == target)
            return false;
        var source = consciousness.OwnedEntity;
        var master = Comp<ChangelingColonyMemberComponent>(target).Mind;
        if (_mind.TryGetMind(target, out _, out _) || HasComp<ActorComponent>(target))
            return false;

        colony.Moving = true;
        try
        {
            if (source is { } old && TryComp<ChangelingColonyMemberComponent>(old, out var member))
            {
                EndCapture(old, member);
                StopHiveBody(old);
            }
            PrepareHiveBody(target, colony);
            _mind.TransferTo(mind, target, ghostCheckOverride: true, createGhost: false);
            if (mind == master)
            {
                colony.Active = target;
                ObserveHiveBody(colony.Core, Comp<ChangelingMindspaceComponent>(colony.Core), target);
            }
            if (source is { } previous && colony.Members.Contains(previous) && !TerminatingOrDeleted(previous))
            {
                ActivateVacantCarrier(previous);
            }
            SyncHiveActions(master, mind);
            RefreshFragmentActions(target, colony);
            return true;
        }
        finally
        {
            colony.Moving = false;
        }
    }

    private bool CanEnterHiveCarrier(EntityUid body)
        => !TerminatingOrDeleted(body) && !HasComp<AbsorbedComponent>(body) && TryComp<ChangelingColonyMemberComponent>(body, out var member)
           && !member.IsBlood && !HasComp<ChangelingMindspaceComponent>(body)
           && (_mobState.IsAlive(body) || !member.IsFragment);
}
