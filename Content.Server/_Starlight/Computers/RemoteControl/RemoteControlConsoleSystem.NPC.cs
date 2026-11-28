using Content.Server.NPC.Systems;
using Content.Shared._Starlight.Computers.RemoteControl;

namespace Content.Server._Starlight.Computers.RemoteControl;

public sealed partial class RemoteControlConsoleSystem
{
    [Dependency] private NPCSystem _npc = default!;

    private void SetRemoteControlNpcState(EntityUid remoteEntity, bool controlled)
    {
        if (TerminatingOrDeleted(remoteEntity))
            return;

        if (controlled)
            EnsureComp<RemoteControlControlledComponent>(remoteEntity);
        else
            RemComp<RemoteControlControlledComponent>(remoteEntity);

        _npc.OnRemoteControlChanged(remoteEntity, controlled);
    }
}
