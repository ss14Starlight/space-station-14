using Content.Server._Starlight.Objectives.Events;
using Content.Server._Starlight.Station;
using Content.Server.GameTicking;
using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Railroading.Events;
using Content.Shared._Starlight.Station;
using Content.Shared.Objectives;
using Content.Shared.Station;
using Content.Shared.Station.Components;

namespace Content.Server._Starlight.Railroading;

public sealed partial class RailroadingCrewRetentionTaskSystem : EntitySystem
{
    [Dependency] private readonly RailroadingSystem _railroading = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly StationCrewStatisticsSystem _stats = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RailroadCrewRetentionTaskComponent, RailroadingCardCompletionQueryEvent>(OnTaskCompletionQuery);
        SubscribeLocalEvent<RailroadCrewRetentionTaskComponent, CollectObjectiveInfoEvent>(OnCollectObjectiveInfo);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRoundEnd, after: [typeof(StationCrewStatisticsSystem)]);
    }

    private void OnRoundEnd(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.PostRound)
            return;

        var query = EntityQueryEnumerator<RailroadCrewRetentionTaskComponent, RailroadCardPerformerComponent>();
        while (query.MoveNext(out _, out var task, out var performer))
        {
            if (performer.Performer is not { } subject)
                continue;

            if (!TryGetStationStats(subject, out var stats) || stats.Crew == 0)
                continue;

            var alive = stats.Crew - stats.LostCrew;
            var ratio = alive > 0 ? (float)stats.EvacuatedCrew / alive : 0f;
            task.Progress = ratio;
            _railroading.InvalidateProgress(subject);
        }
    }

    private void OnCollectObjectiveInfo(Entity<RailroadCrewRetentionTaskComponent> ent, ref CollectObjectiveInfoEvent args)
        => args.Objectives.Add(new ObjectiveInfo
    {
        Title = Loc.GetString(ent.Comp.Message, ("threshold", (int)(ent.Comp.Threshold * 100))),
        Icon = ent.Comp.Icon,
        Progress = ent.Comp.Progress / ent.Comp.Threshold,
    });

    private void OnTaskCompletionQuery(Entity<RailroadCrewRetentionTaskComponent> ent, ref RailroadingCardCompletionQueryEvent args)
    {
        if (args.IsCompleted == false) return;

        args.IsCompleted = ent.Comp.Progress > ent.Comp.Threshold;
    }

    private bool TryGetStationStats(Entity<RailroadableComponent> subject, out StationCrewStatisticsComponent stats)
    {
        stats = default!;

        if (TryComp<StationTrackerComponent>(subject, out var tracker) && tracker.LastStation is { } lastStation)
            return TryComp(lastStation, out stats!);

        var station = _station.GetOwningStation(subject);
        return station is { } stationUid && TryComp(stationUid, out stats!);
    }
}
