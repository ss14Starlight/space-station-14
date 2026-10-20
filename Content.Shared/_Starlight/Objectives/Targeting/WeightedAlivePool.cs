using Content.Shared.Mind;
using Content.Shared.Mind.Filters;

namespace Content.Shared._Starlight.Objectives.Targeting;

/// <summary>
/// Ordinary targets, with extra weight for marked characters.
/// </summary>
public sealed partial class WeightedAlivePool : IMindPool
{
    [DataField]
    public bool WeightHighValue = true;

    public void FindMinds(HashSet<Entity<MindComponent>> minds, EntityUid? exclude,
        IEntityManager entMan, SharedMindSystem mindSys)
    {
        mindSys.AddAliveHumans(minds, exclude);
        mindSys.AddObjectiveTargetNonHumanoids(minds, exclude);
    }

    public int Weight(Entity<MindComponent> mind, IEntityManager entMan)
        => WeightHighValue && HighValueTargetsPool.IsHighValue(mind, entMan)
            ? 2 * HighValueTargetsPool.Weight(mind, entMan) : 1;
}
