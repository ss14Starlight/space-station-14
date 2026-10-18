using System.Linq;
using Content.Server._Starlight.Objectives.Systems;
using Content.Shared.Mind;
using Content.Shared.Mind.Filters;

namespace Content.Server._Starlight.Objectives.Targeting;

public sealed partial class HasHeartMindFilter : MindFilter
{
    protected override bool ShouldRemove(Entity<MindComponent> mind, EntityUid? exclude,
        IEntityManager entMan, SharedMindSystem mindSys)
        => mind.Comp.OwnedEntity is not { } body ||
            !entMan.System<StealHeartConditionSystem>().GetHearts(body).Any();
}
