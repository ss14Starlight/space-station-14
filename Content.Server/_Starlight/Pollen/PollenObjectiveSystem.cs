using System.Linq;
using Content.Server._Starlight.Pollen.Components;
using Content.Shared._Starlight.Pollen.Components;
using Content.Shared.Localizations;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Pollen.Systems;

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

    private void OnGetProgress(Entity<PollenCollectionConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (ent.Comp.Diona is not { } diona || !TryComp<PollenCollectorComponent>(diona, out var collector))
            return;

        args.Progress = Progress(collector.Collected, collector.Pollen.Count);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PollenCollectorComponent>();
        while (query.MoveNext(out var uid, out var collector))
        {
            if (collector.ObjectiveGranted)
                continue;

            if (!_mind.TryGetMind(uid, out var mindId, out var mind))
                continue;

            collector.ObjectiveGranted = true;

            // Diona died and reformed: discard any stale objective still
            // pointing at the old (now-dead) body. Dying means losing your
            // collected pollen progress, so this is a clean slate, not a
            // repoint.
            if (_mind.TryFindObjective((mindId, mind), _pollenObjective.Id, out var stale))
                _mind.TryRemoveObjective(mindId, mind, stale.Value);

            _mind.TryAddObjective(mindId, mind, _pollenObjective.Id);
        }
    }

    private static float Progress(int collected, int total)
    {
        if (total == 0)
            return 1f;

        return MathF.Min(collected / (float)total, 1f);
    }
}
