using Content.Shared._Starlight.Dolls.Events;
using Content.Shared._Starlight.Actions.Components;

namespace Content.Shared.Zombies;

public abstract partial class SharedZombieSystem : EntitySystem
{
    protected void DiscardShell(EntityUid target)
    {
        if(HasComp<ShellComponent>(target))
        {
            var shellEv = new SnapShellPieceEvent
            {
                Performer = target,
                DeShell = true
            };
            RaiseLocalEvent(target, shellEv);
        }
    }
}
