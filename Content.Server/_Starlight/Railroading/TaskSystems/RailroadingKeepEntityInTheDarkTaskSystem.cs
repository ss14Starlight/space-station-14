using Content.Server._Starlight.Objectives.Events;
using Content.Server._Starlight.Shadekin;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Railroading.Events;
using Content.Shared.Mind;
using Content.Shared.Objectives;

namespace Content.Server._Starlight.Railroading;

public sealed partial class RailroadKeepEntityInTheDarkTaskSystem : EntitySystem
{
    [Dependency] private readonly RailroadingSystem _railroading = default!;
    [Dependency] private readonly ShadekinSystem _shadekin = default!;
    [Dependency] private readonly TargetObjectiveSystem _target = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RailroadKeepEntityInTheDarkTaskComponent, RailroadingCardChosenEvent>(OnAfterAssign);
        SubscribeLocalEvent<RailroadKeepEntityInTheDarkTaskComponent, RailroadingCardCompletionQueryEvent>((ent, ref args) => args.IsCompleted = true);
        SubscribeLocalEvent<RailroadKeepEntityInTheDarkTaskComponent, CollectObjectiveInfoEvent>(OnCollectObjectiveInfo);
    }

    private void OnAfterAssign(Entity<RailroadKeepEntityInTheDarkTaskComponent> ent, ref RailroadingCardChosenEvent args)
    {
        if (!TryComp<RailroadableComponent>(args.Subject, out var railroadable)
            || railroadable.ActiveCard is null)
            return;

        _railroading.InvalidateProgress((args.Subject, railroadable));
    }

    private void OnCollectObjectiveInfo(Entity<RailroadKeepEntityInTheDarkTaskComponent> ent, ref CollectObjectiveInfoEvent args)
    {
        if (!HasComp<RailroadCardComponent>(ent.Owner) || !_target.GetTarget(ent.Owner, out var target) || !TryComp<MindComponent>(target.Value, out var mind) || mind.OwnedEntity is null)
            return;

        args.Objectives.Add(new ObjectiveInfo
        {
            Title = _target.GetTitle(target.Value, ent.Comp.Message),
            Icon = ent.Comp.Icon,
            Progress = _shadekin.AreWeInTheDark(mind.OwnedEntity.Value) ? 1.0f : 0.0f,
        });
    }
}
