using Content.Server.Silicons.Laws;
using Content.Shared.Objectives.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Whitelist;

namespace Content.Server._Starlight.Objectives;

public sealed partial class EnsureBorgHasLawsConditionSystem : EntitySystem
{
    [Dependency] private SiliconLawSystem _siliconLaw = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EnsureLawBoundEntitiesHaveNoLawsConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<EnsureLawBoundEntitiesHaveNoLawsConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var query = EntityQueryEnumerator<SiliconLawBoundComponent>();
        var freeBorgs = 0;

        while (query.MoveNext(out var lawBoundEnt, out var lawBound))
        {
            if (!_whitelist.CheckBoth(lawBoundEnt, ent.Comp.LawEntityBlacklist, ent.Comp.LawEntityWhitelist))
                continue;

            var laws = _siliconLaw.GetLaws(lawBoundEnt, lawBound);

            if (laws.Laws.Count == 0)
                freeBorgs++;
        }

        args.Progress = freeBorgs / (float)ent.Comp.EntitiesToFree;
    }
}
