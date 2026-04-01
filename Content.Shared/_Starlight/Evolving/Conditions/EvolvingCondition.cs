using JetBrains.Annotations;

namespace Content.Shared._Starlight.Evolving.Conditions;

/// <summary>
/// Used to define a complicated condition that requires C#
/// </summary>
[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class EvolvingCondition
{
    public abstract EvolveType Type { get; }
    /// <summary>
    /// Determines whether or not a certain entity can evolve.
    /// </summary> 
    /// <returns>Whether or not the entity can evolve</returns>
    public abstract bool Condition(EvolvingConditionArgs args);

    public abstract int GetTarget();
}

public readonly record struct EvolvingConditionArgs(EntityUid Owner, EntityUid? TargetEventsEntity, IEntityManager EntityManager);