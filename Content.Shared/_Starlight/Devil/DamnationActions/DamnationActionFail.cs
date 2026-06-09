namespace Content.Shared._Starlight.Devil.DamnationActions;

// test class
public sealed partial class DamnationActionFail : DamnationAction
{
    public override bool Action(Entity<DamnedComponent> victim) => false;
}
