using Content.Shared.Mind;
using Content.Shared.Mind.Filters;
using Content.Shared.Objectives.Systems;

namespace Content.Shared._Starlight.Objectives.Targeting;

/// <summary>
/// Ordinary targets, with extra weight for marked characters.
/// </summary>
public sealed partial class WeightedAlivePool : MindPool
{
    [DataField]
    public bool WeightHighValue = true;

    public override void FindMinds(HashSet<Entity<MindComponent>> minds, EntityUid? exclude,
        IEntityManager entMan, TargetSystem targetSys)
    {
        targetSys.AddAliveHumans(minds, exclude);
        targetSys.AddObjectiveTargetNonHumanoids(minds, exclude);
    }

    public int Weight(Entity<MindComponent> mind, IEntityManager entMan)
        => WeightHighValue && HighValueTargetsPool.IsHighValue(mind, entMan)
            ? 2 * HighValueTargetsPool.Weight(mind, entMan) : 1;
}
