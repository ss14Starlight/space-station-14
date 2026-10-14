using Content.Server._Starlight.Terminator;
using Content.Shared._Starlight.Devil;
using Content.Shared._Starlight.Devil.DamnationActions;

namespace Content.Server._Starlight.Devil.DamnationActions;

public sealed partial class DamnationActionTerminator : DamnationAction
{
    private TerminatorSystem _terminator = default!;

    public override bool Action(Entity<DamnedComponent> victim) => _terminator.CreateTerminator(victim.Owner);

    public override void ResolveIoC()
    {
        base.ResolveIoC();

        _terminator = _entityManager.System<TerminatorSystem>();
    }
}
