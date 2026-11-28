using Content.Shared.Mind.Components;

// ReSharper disable CheckNamespace

namespace Content.Server.NPC.Systems;

public sealed partial class NPCSystem
{
    public void OnRemoteControlChanged(EntityUid uid, bool controlled)
    {
        if (controlled)
        {
            SleepNPC(uid);
            return;
        }

        if (_mobState.IsIncapacitated(uid) || TerminatingOrDeleted(uid))
            return;

        if (TryComp<MindContainerComponent>(uid, out var mindContainer) && mindContainer.HasMind)
            return;

        WakeNPC(uid);
    }
}
