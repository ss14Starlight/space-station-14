using Content.Shared.Mobs;

namespace Content.Shared._Starlight.Devil.Damnations;

[ImplicitDataDefinitionForInheritors]
public abstract partial class DamnationAction
{
    public abstract bool Action(Entity<DamnedComponent> victim);
    public virtual bool ReverseAction(Entity<DamnedComponent> victim) => true;

    public bool IocResolved = false;
    public abstract void ResolveIoC();
}