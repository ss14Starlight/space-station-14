using Content.Shared._Starlight.Devil.DamnationActions;
using Content.Shared._Starlight.Devil;

namespace Content.Server._Starlight.Devil.DamnationActions;

public sealed partial class DamnationActionFail : DamnationAction
{
    public override bool Action(Entity<DamnedComponent> victim) => false;
}
