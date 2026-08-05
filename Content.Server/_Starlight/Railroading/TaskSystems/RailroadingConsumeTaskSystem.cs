using System.Linq;
using Content.Server._Starlight.Objectives.Events;
using Content.Shared._Starlight.Railroading.Components;
using Content.Shared._Starlight.Railroading.Components.Tasks;
using Content.Shared._Starlight.Railroading.Components.Watchers;
using Content.Shared._Starlight.Railroading.Events;
using Content.Shared.Nutrition;
using Content.Shared.Objectives;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Railroading.TaskSystems;

public sealed partial class RailroadingConsumeTaskSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private RailroadingSystem _railroading = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RailroadConsumeTaskComponent, RailroadingCardChosenEvent>(OnConsumeTaskPicked);
        SubscribeLocalEvent<RailroadConsumeTaskComponent, RailroadingCardCompletionQueryEvent>(OnConsumeTaskCompletionQuery);
        SubscribeLocalEvent<RailroadConsumeTaskComponent, CollectObjectiveInfoEvent>(OnCollectObjectiveInfo);

        SubscribeLocalEvent<RailroadConsumeWatcherComponent, ConsumedFoodEvent>(OnFullyEaten);
    }

    private void OnFullyEaten(Entity<RailroadConsumeWatcherComponent> ent, ref ConsumedFoodEvent args)
    {
        if (!TryComp<RailroadableComponent>(ent, out var railroadable)
            || railroadable.ActiveCard is null
            || !TryComp<RailroadConsumeTaskComponent>(railroadable.ActiveCard, out var task))
            return;

        if (task.Objects.Contains(args.Food))
        {
            task.IsCompleted = true;
            RemComp<RailroadConsumeWatcherComponent>(ent);
            _railroading.InvalidateProgress((ent, railroadable));
        }
    }

    private void OnCollectObjectiveInfo(Entity<RailroadConsumeTaskComponent> ent, ref CollectObjectiveInfoEvent args)
    {
        var prototype = _proto.Index(ent.Comp.Objects.FirstOrDefault());
        args.Objectives.Add(new ObjectiveInfo
        {
            Title = Loc.GetString(ent.Comp.Message, ("Target", Loc.GetString(prototype.Name))),
            Icon = ent.Comp.Icon,
            Progress = ent.Comp.IsCompleted ? 1.0f : 0.0f,
        });
    }

    private void OnConsumeTaskCompletionQuery(Entity<RailroadConsumeTaskComponent> ent, ref RailroadingCardCompletionQueryEvent args)
    {
        if (args.IsCompleted == false) return;

        args.IsCompleted = ent.Comp.IsCompleted;
    }

    private void OnConsumeTaskPicked(Entity<RailroadConsumeTaskComponent> ent, ref RailroadingCardChosenEvent args)
        => EnsureComp<RailroadConsumeWatcherComponent>(args.Subject.Owner);
}
