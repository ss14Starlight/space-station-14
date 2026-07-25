using JetBrains.Annotations;

namespace Content.Shared._Starlight.Abstract.Conditions;
/// <summary>
/// Used to define a complicated condition that requires C#
/// </summary>
[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class BaseCondition
{
    [Dependency] protected IEntityManager Ent = default!;

    public virtual bool Handle(EntityUid @subject, EntityUid @object)
    {
        if (Ent == null)
            IoCManager.InjectDependencies(this);
        return false;
    }
}
