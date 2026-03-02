using Content.Shared._Starlight.Evolving.EntitySystems;
using Content.Shared._Starlight.Evolving;
using Content.Shared._Starlight.Evolving.Conditions;
using Content.Shared.Mind;

namespace Content.Client._Starlight.Evolving.EntitySystems;

public sealed class EvolvingSystem : SharedEvolvingSystem
{
    public override EntityUid TryInitObjectives(EntityUid mindId, MindComponent mind, string objectiveId, EvolvingCondition condition) => base.TryInitObjectives(mindId, mind, objectiveId, condition);
}