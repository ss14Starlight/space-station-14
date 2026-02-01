using Content.Server._Starlight.Objectives.Events;
using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Railroading.Events;
using Content.Shared._Starlight.Shadekin;
using Content.Shared.Objectives;

namespace Content.Server._Starlight.Railroading;

public sealed partial class RailroadingSupercritPortalSystem : EntitySystem
{
    [Dependency] private readonly RailroadingSystem _railroading = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RailroadSupercritPortalTaskComponent, RailroadingCardChosenEvent>(OnTaskPicked);
        SubscribeLocalEvent<RailroadSupercritPortalTaskComponent, RailroadingCardCompletionQueryEvent>(OnTaskCompletionQuery);
        SubscribeLocalEvent<RailroadSupercritPortalTaskComponent, CollectObjectiveInfoEvent>(OnCollectObjectiveInfo);
    }

    public void SupercriticalTask(EntityUid uid)
    {
        if (!TryComp<RailroadableComponent>(uid, out var railroadable)
            || railroadable.ActiveCard is null
            || !TryComp<RailroadSupercritPortalTaskComponent>(railroadable.ActiveCard, out var task))
            return;

        task.IsCompleted = true;
        RemComp<RailroadSupercritPortalWatcherComponent>(uid);
        _railroading.InvalidateProgress((uid, railroadable));
    }

    private void OnCollectObjectiveInfo(Entity<RailroadSupercritPortalTaskComponent> ent, ref CollectObjectiveInfoEvent args)
    {
        if (!HasComp<RailroadCardComponent>(ent.Owner))
            return;

        args.Objectives.Add(new ObjectiveInfo
        {
            Title = Loc.GetString(ent.Comp.Message),
            Icon = ent.Comp.Icon,
            Progress = ent.Comp.IsCompleted ? 1.0f : 0.0f,
        });
    }

    private void OnTaskCompletionQuery(Entity<RailroadSupercritPortalTaskComponent> ent, ref RailroadingCardCompletionQueryEvent args)
    {
        if (args.IsCompleted == false) return;

        args.IsCompleted = ent.Comp.IsCompleted;
    }

    private void OnTaskPicked(Entity<RailroadSupercritPortalTaskComponent> ent, ref RailroadingCardChosenEvent args) 
        => EnsureComp<RailroadSupercritPortalWatcherComponent>(args.Subject.Owner);
}
