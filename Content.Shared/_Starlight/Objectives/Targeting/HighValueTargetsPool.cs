using Content.Shared.Mind;
using Content.Shared.Mind.Filters;
using Content.Shared._Starlight.Traits.Antags;
using Content.Shared.Objectives.Systems;

namespace Content.Shared._Starlight.Objectives.Targeting;

/// <summary>
/// Living command staff and characters who opted into being high value targets.
/// </summary>
public sealed partial class HighValueTargetsPool : MindPool
{
    [DataField]
    public bool IncludeCommand = true;

    public override void FindMinds(HashSet<Entity<MindComponent>> minds, EntityUid? exclude,
        IEntityManager entMan, TargetSystem targetSys)
    {
        targetSys.AddAliveHumans(minds, exclude);
        targetSys.AddObjectiveTargetNonHumanoids(minds, exclude);
        minds.RemoveWhere(mind => mind.Comp.OwnedEntity is not { } body ||
            (!(IncludeCommand && IsCommand(body, entMan)) && !entMan.HasComponent<MarkedForDeathComponent>(body)));
    }

    public static int Weight(Entity<MindComponent> mind, IEntityManager entMan)
        => mind.Comp.OwnedEntity is { } body && IsCommand(body, entMan) &&
            entMan.HasComponent<MarkedForDeathComponent>(body) ? 2 : 1;

    public static bool IsHighValue(Entity<MindComponent> mind, IEntityManager entMan)
        => mind.Comp.OwnedEntity is { } body &&
            (IsCommand(body, entMan) || entMan.HasComponent<MarkedForDeathComponent>(body));

    private static bool IsCommand(EntityUid body, IEntityManager entMan)
        => entMan.ComponentFactory.TryGetRegistration("CommandStaff", out var registration) &&
            entMan.HasComponent(body, registration.Type);
}
