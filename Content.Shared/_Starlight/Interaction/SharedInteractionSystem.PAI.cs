using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared.PAI;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Interaction;

public abstract partial class SharedInteractionSystem
{
    private bool IsSlottedPAI(EntityUid actor, EntityUid target)
    {
        return HasComp<PAIComponent>(actor) &&
               _containerSystem.TryGetContainingContainer(actor, out var container) &&
               container != null &&
               container.Owner == target &&
               (container.ID == "pai_slot" || container.ID == "PDA-pai");
    }

    private EntityUid GetPAIRemoteUiInteractionActor(EntityUid actor, EntityUid target, Enum uiKey, bool isOpenAttempt)
    {
        if (!HasComp<PAIComponent>(actor) || !_actionBlockerSystem.CanConsciouslyPerformAction(actor))
            return actor;

        var ev = new RemoteControlUiInteractionActorEvent(actor, target, uiKey, isOpenAttempt);
        RaiseLocalEvent(ref ev);
        return ev.Actor ?? actor;
    }
}
