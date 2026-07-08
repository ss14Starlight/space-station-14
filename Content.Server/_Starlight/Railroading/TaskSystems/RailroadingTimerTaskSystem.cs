using Content.Server._Starlight.Objectives.Events;
using Content.Shared._Starlight.Railroading.Components.Tasks;
using Content.Shared._Starlight.Railroading.Events;
using Content.Shared.Objectives;
using Robust.Shared.Timing;
using Content.Shared._Starlight.Abstract;
using Content.Shared._Starlight.Railroading.Components;

namespace Content.Server._Starlight.Railroading.TaskSystems;

public sealed partial class RailroadingTimerTaskSystem : AccUpdateEntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RailroadingSystem _railroading = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RailroadTimerTaskComponent, RailroadingCardChosenEvent>(OnTaskPicked);
        SubscribeLocalEvent<RailroadTimerTaskComponent, RailroadingCardCompletionQueryEvent>(OnTaskCompletionQuery);
        SubscribeLocalEvent<RailroadTimerTaskComponent, CollectObjectiveInfoEvent>(OnCollectObjectiveInfo);
    }

    protected override void AccUpdate(float _)
    {
        var query = AllEntityQuery<RailroadTimerTaskComponent>();
        while (query.MoveNext(out var ent, out var comp))
        {
            if (comp.IsCompleted) continue;
            if (comp.EndTime <= _timing.CurTime
                && TryComp<RailroadCardPerformerComponent>(ent, out var performer)
                && performer.Performer is Entity<RailroadableComponent> railroadable)
            {
                comp.IsCompleted = true;
                _railroading.InvalidateProgress(railroadable);
            }
        }
    }

    private void OnCollectObjectiveInfo(Entity<RailroadTimerTaskComponent> ent, ref CollectObjectiveInfoEvent args)
    => args.Objectives.Add(new ObjectiveInfo
    {
        Title = Loc.GetString(ent.Comp.Message, ("duration", ent.Comp.Duration.TotalMinutes)),
        Icon = ent.Comp.Icon,
        Progress = ent.Comp.IsCompleted
                ? 1.0f
                : Math.Clamp((float)((_timing.CurTime - ent.Comp.Started).TotalSeconds / (ent.Comp.EndTime - ent.Comp.Started).TotalSeconds), 0.0f, 1.0f)
    });

    private void OnTaskCompletionQuery(Entity<RailroadTimerTaskComponent> ent, ref RailroadingCardCompletionQueryEvent args)
    {
        if (args.IsCompleted == false) return;

        args.IsCompleted = ent.Comp.IsCompleted;
    }
    private void OnTaskPicked(Entity<RailroadTimerTaskComponent> ent, ref RailroadingCardChosenEvent args)
    {
        ent.Comp.Started = _timing.CurTime;
        ent.Comp.EndTime = ent.Comp.Started + ent.Comp.Duration;
    }
}
