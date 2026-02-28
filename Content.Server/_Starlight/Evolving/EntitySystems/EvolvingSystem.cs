using System.Linq;
using Content.Shared._Starlight.Evolving;
using Content.Shared._Starlight.Weapons.Melee.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared._Starlight.Evolving.Conditions;
using Content.Shared._Starlight.Evolving.EntitySystems;
using Content.Shared.Actions;
using Content.Shared._Starlight.Antags.TerrorSpider;
using Content.Shared._Starlight.Spider.Events;
using Content.Shared.Objectives.Systems;
using Content.Shared.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server.Objectives.Components;

namespace Content.Server._Starlight.Evolving.EntitySystems;

public sealed class EvolvingSystem : SharedEvolvingSystem
{
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectivesSystem = default!;
    [Dependency] private readonly NumberObjectiveSystem _numberObjectiveSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EvolveConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    #region Logic

    private void OnGetProgress(Entity<EvolveConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (!TryComp<NumberObjectiveComponent>(ent.Owner, out var objective))
            return;
        args.Progress = Math.Clamp((float)ent.Comp.Count / objective.Target, 0f, 1f);
    }

    public override EntityUid TryInitObjectives(EntityUid mindId, MindComponent mind, string objectiveId, EvolvingCondition condition)
    {
        var obj = _objectivesSystem.TryCreateObjective(mindId, mind, objectiveId);
        if (obj is not { Valid: true } objEnt)
            return EntityUid.Invalid;

        if (TryComp<EvolveConditionComponent>(objEnt, out var evolveCondition))
            evolveCondition.ConditionType = condition.Type;

        _numberObjectiveSystem.SetTarget(objEnt, condition.GetTarget()); // All evolve conditions are count based with target 1.
        _numberObjectiveSystem.SetTitle(objEnt, $"objective-{condition.Type.ToString().ToLower()}-condition-title");
        _numberObjectiveSystem.SetDescription(objEnt, $"objective-{condition.Type.ToString().ToLower()}-condition-description");

        return objEnt;
    }
    #endregion
}