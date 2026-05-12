using Content.Server.GameTicking;
using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Station;
using Content.Shared.Roles;
using Content.Shared.Station;
using Content.Shared.StationRecords;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Station;

public sealed class StationCrewStatisticsSystem : EntitySystem
{
    [Dependency] private readonly SharedStationRecordsSystem _records = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

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
        var stationXform = Transform(station);

        station.Comp.Clear();

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
            if (xform.MapID != stationXform.MapID)
            {
                if (isBorg)
                    station.Comp.StolenBorgs++;
                else
                    station.Comp.EvacuatedCrew++;
            }

        }
    }
}
