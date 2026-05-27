using Content.Server._Starlight.Objectives.Events;
using Content.Server._Starlight.Station;
using Content.Server.GameTicking;
using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Railroading.Events;
using Content.Shared._Starlight.Station;
using Content.Shared.Objectives;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Robust.Shared.Map;

namespace Content.Server._Starlight.Railroading;

public sealed partial class RailroadingDesertionTaskSystem : EntitySystem
{
    [Dependency] private readonly RailroadingSystem _railroading = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RailroadDesertionTaskComponent, RailroadingCardCompletionQueryEvent>(OnTaskCompletionQuery);
        SubscribeLocalEvent<RailroadDesertionTaskComponent, CollectObjectiveInfoEvent>(OnCollectObjectiveInfo);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRoundEnd);
    }

    private void OnRoundEnd(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.PostRound)
            return;

        var query = EntityQueryEnumerator<RailroadDesertionTaskComponent, RailroadCardPerformerComponent>();
        while (query.MoveNext(out _, out var task, out var performer))
        {
            if (task.IsCompleted || performer.Performer is not { } subject)
                continue;

            if (IsOnStation(subject))
            {
                task.IsCompleted = true;
                _railroading.InvalidateProgress(subject);
            }
        }
    }

    private void OnCollectObjectiveInfo(Entity<RailroadDesertionTaskComponent> ent, ref CollectObjectiveInfoEvent args)
        => args.Objectives.Add(new ObjectiveInfo
        {
            Title = Loc.GetString(ent.Comp.Message),
            Icon = ent.Comp.Icon,
            Progress = ent.Comp.IsCompleted ? 1.0f : 0.0f,
        });

    private void OnTaskCompletionQuery(Entity<RailroadDesertionTaskComponent> ent, ref RailroadingCardCompletionQueryEvent args)
        => args.IsCompleted = ent.Comp.IsCompleted;

    private bool IsOnStation(Entity<RailroadableComponent> subject)
    {
        EntityUid? stationUid = null;

        if (TryComp<StationTrackerComponent>(subject, out var tracker))
            stationUid = tracker.LastStation;

        stationUid ??= _station.GetOwningStation(subject);

        if (stationUid is not { } station
            || !TryComp<StationDataComponent>(station, out var data))
            return false;

        var subjectMap = Transform(subject).MapID;

        foreach (var gridUid in data.Grids)
        {
            if (TryComp<TransformComponent>(gridUid, out var xform))
                return subjectMap == xform.MapID;
        }

        return false;
    }
}
