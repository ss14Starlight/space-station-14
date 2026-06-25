using JetBrains.Annotations;
using Robust.Shared.Player;

namespace Content.Shared._Starlight.Abstract.Conditions;

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class BaseRequirement
{
    [Dependency] protected IEntityManager Ent = default!;

    public virtual string GetRequirementDescription()
    {
        if (Ent == null)
            IoCManager.InjectDependencies(this);
        return "";
    }

    public virtual bool Handle(ICommonSession user)
    {
        if (Ent == null)
            IoCManager.InjectDependencies(this);
        return false;
    }
}
