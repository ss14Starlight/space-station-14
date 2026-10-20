using System.Linq;
using Content.Server._Starlight.Pollen.Components;
using Content.Shared._Starlight.Pollen.Components;
using Content.Shared.Localizations;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Objectives.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Pollen;

public sealed partial class PollenObjectiveSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private PollenCatalogSystem _catalog = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    private static readonly EntProtoId<ObjectiveComponent> _pollenObjective = "PollenCollectionObjective";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PollenCollectionConditionComponent, RequirementCheckEvent>(OnRequirementCheck);
        SubscribeLocalEvent<PollenCollectionConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<PollenCollectorComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<PollenCollectionConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnRequirementCheck(EntityUid uid, PollenCollectionConditionComponent comp, ref RequirementCheckEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.Mind.CurrentEntity is not { } currentEntity ||
            !HasComp<PollenCollectorComponent>(currentEntity))
        {
            args.Cancelled = true;
            return;
        }

        comp.Diona = currentEntity;
    }

    private void OnAfterAssign(EntityUid uid, PollenCollectionConditionComponent comp, ref ObjectiveAfterAssignEvent args)
    {
        if (comp.Diona is not { } diona || !TryComp<PollenCollectorComponent>(diona, out var collector))
            return;

        var names = collector.Pollen.Keys.Select(_catalog.GetName).ToList();
        var description = Loc.GetString("objective-condition-pollen-collection-desc",
            ("plants", ContentLocalizationManager.FormatList(names)));

        _metaData.SetEntityDescription(uid, description, args.Meta);
    }

    private void OnMindAdded(Entity<PollenCollectorComponent> ent, ref MindAddedMessage args)
    {
        if (!_mind.TryGetMind(ent.Owner, out var mindId, out var mind))
            return;

        if (ent.Comp.ObjectiveGrantedMind == mindId)
            return;

        ent.Comp.ObjectiveGrantedMind = mindId;

        if (_mind.TryFindObjective((mindId, mind), _pollenObjective.Id, out var stale))
            _mind.TryRemoveObjective(mindId, mind, stale.Value);

        _mind.TryAddObjective(mindId, mind, _pollenObjective.Id);
    }

    private void OnGetProgress(Entity<PollenCollectionConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (ent.Comp.Diona is not { } diona || !TryComp<PollenCollectorComponent>(diona, out var collector))
            return;

        args.Progress = Progress(collector.Collected, collector.Pollen.Count);
    }

    private static float Progress(int collected, int total)
    {
        if (total == 0)
            return 1f;

        return MathF.Min(collected / (float)total, 1f);
    }
}
