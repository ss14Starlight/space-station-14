using Content.Server._Starlight.Objectives.Events;
using Content.Server._Starlight.Railroading;
using Content.Server.Objectives.Components;
using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Railroading.Events;
using Content.Shared.Mind;
using Content.Shared.Objectives;
using Content.Shared.Objectives.Components;

namespace Content.Server.Objectives.Systems;

/// <summary>
/// Handles keep alive condition logic.
/// </summary>
public sealed class KeepAliveConditionSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly TargetObjectiveSystem _target = default!;
    [Dependency] private readonly RailroadingSystem _railroad = default!; // Starlight

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KeepAliveConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);

        SubscribeLocalEvent<KeepAliveConditionComponent, CollectObjectiveInfoEvent>(OnCollectObjectiveInfo); // Starlight
        SubscribeLocalEvent<KeepAliveConditionComponent, RailroadingCardChosenEvent>(OnAfterAssign); // Starlight
        SubscribeLocalEvent<KeepAliveConditionComponent, RailroadingCardCompletionQueryEvent>((ent, ref args) => args.IsCompleted = true); // Starlight
    }

    // Starlight - Start
    private void OnAfterAssign(Entity<KeepAliveConditionComponent> ent, ref RailroadingCardChosenEvent args)
    {
        if (!TryComp<RailroadableComponent>(args.Subject, out var railroadable)
            || railroadable.ActiveCard is null)
            return;

        _railroad.InvalidateProgress((args.Subject, railroadable));
    }

    private void OnCollectObjectiveInfo(Entity<KeepAliveConditionComponent> ent, ref CollectObjectiveInfoEvent args)
    {
        if (!HasComp<RailroadCardComponent>(ent.Owner) || !TryComp<TargetObjectiveComponent>(ent.Owner, out var target) || target.Target is null)
            return;

        args.Objectives.Add(new ObjectiveInfo
        {
            Title = target.Title,
            Icon = target.Icon,
            Progress = GetProgress(target.Target.Value),
        });
    }

    // Starlight - End

    private void OnGetProgress(EntityUid uid, KeepAliveConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (!_target.GetTarget(uid, out var target))
            return;

        args.Progress = GetProgress(target.Value);
    }

    private float GetProgress(EntityUid target)
    {
        if (!TryComp<MindComponent>(target, out var mind))
            return 0f;

        return _mind.IsCharacterDeadIc(mind) ? 0f : 1f;
    }
}
