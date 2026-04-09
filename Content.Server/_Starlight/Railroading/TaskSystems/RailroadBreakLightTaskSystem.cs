using Content.Server._Starlight.Objectives.Events;
using Content.Server._Starlight.Shadekin;
using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Railroading.Events;
using Content.Shared.Abilities.Goliath;
using Content.Shared.Damage.Systems;
using Content.Shared.Light.Components;
using Content.Shared.Objectives;
using Content.Shared.Station.Components;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Railroading;

public sealed partial class RailroadBreakLightTaskSystem : EntitySystem
{
    [Dependency] private readonly RailroadingSystem _railroading = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RailroadBreakLightTaskComponent, RailroadingCardChosenEvent>(OnTaskPicked);
        SubscribeLocalEvent<RailroadBreakLightTaskComponent, RailroadingCardCompletionQueryEvent>(OnTaskCompletionQuery);
        SubscribeLocalEvent<RailroadBreakLightTaskComponent, CollectObjectiveInfoEvent>(OnCollectObjectiveInfo);

        SubscribeLocalEvent<RailroadBreakLightWatcherComponent, OnLightBreakEvent>(OnLightBreakEvent);
    }

    public void OnLightBreakEvent(EntityUid uid, RailroadBreakLightWatcherComponent component, OnLightBreakEvent args)
    {
        if (!TryComp<RailroadableComponent>(uid, out var railroadable)
             || railroadable.ActiveCard is null
             || !TryComp<RailroadBreakLightTaskComponent>(railroadable.ActiveCard, out var task))
            return;

        task.LightBroken += 1;

        if (task.LightBroken >= task.Target)
        {
            task.IsCompleted = true;
            RemComp<RailroadBreakLightWatcherComponent>(uid);
            _railroading.InvalidateProgress((uid, railroadable));
        }
    }

    private void OnCollectObjectiveInfo(Entity<RailroadBreakLightTaskComponent> ent, ref CollectObjectiveInfoEvent args)
    {
        if (!HasComp<RailroadCardComponent>(ent.Owner))
            return;

        args.Objectives.Add(new ObjectiveInfo
        {
            Title = Loc.GetString(ent.Comp.Message, ("Amount", ent.Comp.Target)),
            Icon = ent.Comp.Icon,
            Progress = Math.Clamp(ent.Comp.LightBroken / ent.Comp.Target, 0.0f, 1.0f)
        });
    }

    private void OnTaskCompletionQuery(Entity<RailroadBreakLightTaskComponent> ent, ref RailroadingCardCompletionQueryEvent args)
    {
        if (args.IsCompleted == false) return;

        args.IsCompleted = ent.Comp.IsCompleted;
    }

    private void OnTaskPicked(Entity<RailroadBreakLightTaskComponent> ent, ref RailroadingCardChosenEvent args)
    {
        ent.Comp.Target = ent.Comp.Amount.Next(_random);
        EnsureComp<RailroadBreakLightWatcherComponent>(args.Subject.Owner);
    }
}
