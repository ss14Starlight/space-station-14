using Content.Shared._Starlight.Cuffs;
using Content.Shared._Starlight.Dolls.Events;
using Content.Shared._Starlight.Actions.Components;

// ReSharper disable CheckNamespace
namespace Content.Shared.Zombies;

public abstract partial class SharedZombieSystem : EntitySystem
{

    [SubscribeLocalEvent]
    private void OnCuffedAttackAttempt(Entity<ZombieComponent> ent, ref CuffedAttackAttemptEvent args)
    {
        if (!args.Disarm && args.Weapon?.Owner == ent.Owner)
            args.Allowed = true;
    }

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
