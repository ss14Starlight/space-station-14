using Content.Server.GameTicking;
using Content.Shared._Starlight.Station;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Content.Shared.StationRecords;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Station;

public sealed partial class StationCrewStatisticsSystem : EntitySystem
{
    [Dependency] private SharedStationRecordsSystem _records = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
        => SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRoundEnd);

    private void OnRoundEnd(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.PostRound)
            return;

        var query = EntityQueryEnumerator<StationCrewStatisticsComponent>();

        while (query.MoveNext(out var station, out var comp))
        {
            CheckStation((station, comp));
        }
    }

    private void CheckStation(Entity<StationCrewStatisticsComponent> station, StationRecordsComponent? records = null)
    {
        if (!Resolve(station, ref records, false))
            return;

        station.Comp.Clear();

        // The station data entity lives in nullspace, so compare against its grids' maps, not Transform(station).
        if (!TryComp<StationDataComponent>(station, out var stationData))
            return;

        // Main station grid check. If main grids is (somehow) empty, fallback to any grid in station data.
        var grids = stationData.MainGrids.Count > 0
            ? stationData.MainGrids
            : stationData.Grids;

        var stationMaps = new HashSet<MapId>();
        foreach (var gridUid in grids)
        {
            if (TryComp<TransformComponent>(gridUid, out var gridXform))
                stationMaps.Add(gridXform.MapID);
        }

        if (stationMaps.Count == 0)
            return;

        foreach (var (id, record) in _records.GetRecordsOfType<GeneralStationRecord>(station, records))
        {
            if (!_proto.TryIndex<JobPrototype>(record.JobPrototype, out var job))
                continue;

            if (job.ID == "StationAi")
                continue;

            var isBorg = job.ID == "Borg";

            if (isBorg)
                station.Comp.Borgs++;
            else
                station.Comp.Crew++;

            if (record.Entity is null || !TryGetEntity(record.Entity.Value, out var ent) || TerminatingOrDeleted(ent))
            {
                if (isBorg)
                    station.Comp.LostBorgs++;
                else
                    station.Comp.LostCrew++;
                continue;
            }

            var xform = Transform(ent.Value);
            if (!stationMaps.Contains(xform.MapID))
            {
                if (isBorg)
                    station.Comp.StolenBorgs++;
                else
                    station.Comp.EvacuatedCrew++;
            }
        }
    }
}
