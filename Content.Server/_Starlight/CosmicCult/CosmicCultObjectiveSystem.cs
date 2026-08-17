using Content.Server.Objectives.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Content.Shared.Warps;
using Content.Server._Starlight.CosmicCult.Components;
using Content.Shared._Starlight.CosmicCult.Roles;
using Robust.Shared.Random;
using Content.Server.Station.Systems;
using Content.Server._Starlight.CosmicCult.EntitySystems;

namespace Content.Server._Starlight.CosmicCult;

public sealed partial class CosmicCultObjectiveSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private NumberObjectiveSystem _number = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private CosmicMalignEmpoweredRiftSystem _riftSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicEffigyConditionComponent, RequirementCheckEvent>(OnEffigyRequirementCheck);
        SubscribeLocalEvent<CosmicEffigyConditionComponent, ObjectiveAfterAssignEvent>(OnEffigyAfterAssign);

        SubscribeLocalEvent<CosmicEntropyConditionComponent, ObjectiveGetProgressEvent>(OnGetEntropyProgress);
        SubscribeLocalEvent<CosmicConversionConditionComponent, ObjectiveGetProgressEvent>(OnGetConversionProgress);
        SubscribeLocalEvent<CosmicTierConditionComponent, ObjectiveGetProgressEvent>(OnGetTierProgress);
        SubscribeLocalEvent<CosmicVictoryConditionComponent, ObjectiveGetProgressEvent>(OnGetVictoryProgress);
        SubscribeLocalEvent<CosmicChaplainConditionComponent, ObjectiveGetProgressEvent>(OnGetChaplainProgress);
        SubscribeLocalEvent<CosmicSacrificedCrewConditionComponent, ObjectiveGetProgressEvent>(OnGetSacrificedCrewProgress);
    }


    private void OnEffigyRequirementCheck(EntityUid uid, CosmicEffigyConditionComponent comp, ref RequirementCheckEvent args)
    {
        if (args.Cancelled || !_roles.MindHasRole<CosmicColossusRoleComponent>(args.MindId))
            return;

        if (args.Mind.CurrentEntity is not { } currentEntity)
        {
            args.Cancelled = true;
            return;
        }

        var xform = Transform(currentEntity);

        // Colossus must spawn on a grid.
        if (xform.GridUid is not { } currentGrid)
        {
            args.Cancelled = true;
            return;
        }

        var station = _station.GetOwningStation(currentEntity);
        if (station is null)
        {
            args.Cancelled = true;
            return;
        }

        var warps = new List<EntityUid>();
        var query = EntityQueryEnumerator<WarpPointComponent>();

        // First: try beacons on the Colossus's current grid.
        while (query.MoveNext(out var warpUid, out var warp))
        {
            if (warp.Location == null)
                continue;

            if (Transform(warpUid).GridUid != currentGrid)
                continue;

            if (_station.GetOwningStation(warpUid) != station)
                continue;

            warps.Add(warpUid);
        }

        // No beacon on the current grid: fall back to the station's largest grid.
        if (warps.Count == 0)
        {
            var stationGrid = _station.GetLargestGrid((station.Value, null));

            if (stationGrid is null)
            {
                args.Cancelled = true;
                return;
            }

            query = EntityQueryEnumerator<WarpPointComponent>();

            while (query.MoveNext(out var warpUid, out var warp))
            {
                if (warp.Location == null)
                    continue;

                if (Transform(warpUid).GridUid != stationGrid)
                    continue;

                if (_station.GetOwningStation(warpUid) != station)
                    continue;

                warps.Add(warpUid);
            }
        }
        // No valid beacon found.
        if (warps.Count == 0)
        {
            args.Cancelled = true;
            return;
        }

        comp.EffigyTarget = _random.Pick(warps);
    }

    private void OnEffigyAfterAssign(EntityUid uid, CosmicEffigyConditionComponent comp, ref ObjectiveAfterAssignEvent args)
    {
        string description;
        if (comp.EffigyTarget == null || !TryComp<WarpPointComponent>(comp.EffigyTarget, out var warp) || warp.Location == null)
        {
            // this should never really happen but eh
            description = Loc.GetString("objective-condition-effigy-no-target");
        }
        else
        {
            description = Loc.GetString("objective-condition-effigy", ("location", warp.Location));
        }
        _metaData.SetEntityDescription(uid, description, args.Meta);
    }

    private void OnGetSacrificedCrewProgress(Entity<CosmicSacrificedCrewConditionComponent> ent, ref ObjectiveGetProgressEvent args)
        => args.Progress = Progress(_riftSystem.StoredCorpseCount, _number.GetTarget(ent.Owner));

    private void OnGetEntropyProgress(Entity<CosmicEntropyConditionComponent> ent, ref ObjectiveGetProgressEvent args)
        => args.Progress = Progress(ent.Comp.Siphoned, _number.GetTarget(ent.Owner));

    private void OnGetConversionProgress(Entity<CosmicConversionConditionComponent> ent, ref ObjectiveGetProgressEvent args)
        => args.Progress = Progress(ent.Comp.Converted, _number.GetTarget(ent.Owner));

    private void OnGetTierProgress(Entity<CosmicTierConditionComponent> ent, ref ObjectiveGetProgressEvent args)
        => args.Progress = Progress(ent.Comp.Tier, _number.GetTarget(ent.Owner));

    private void OnGetVictoryProgress(Entity<CosmicVictoryConditionComponent> ent, ref ObjectiveGetProgressEvent args)
        => args.Progress = ent.Comp.Victory ? 1f : 0f;

    private void OnGetChaplainProgress(Entity<CosmicChaplainConditionComponent> ent, ref ObjectiveGetProgressEvent args)
        => args.Progress = Progress(ent.Comp.Converted, _number.GetTarget(ent.Owner));

    private static float Progress(int recruited, int target)
    {
        // prevent divide-by-zero
        if (target == 0)
            return 1f;

        return MathF.Min(recruited / (float)target, 1f);
    }
}
