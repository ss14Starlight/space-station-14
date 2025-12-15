
namespace Content.Shared._Starlight.Devil.DamnationActions;

[ImplicitDataDefinitionForInheritors]
public abstract partial class DamnationAction
{
    public virtual bool Action(Entity<DamnedComponent> victim) => true;
    public virtual bool ReverseAction(Entity<DamnedComponent> victim) => true;

    public bool IocResolved = false;
    public virtual void ResolveIoC()
    {
        return;
    }
}