using Content.Server._Starlight.Objectives.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;

namespace Content.Server._Starlight.Objectives.Systems;

public sealed partial class AchieveObjectivesConditionSystem : EntitySystem
{
    [Dependency] private SharedObjectivesSystem _objectives = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AchieveObjectivesConditionComponent, ObjectiveGetProgressEvent>(OnProgress);
    }

    private void OnProgress(Entity<AchieveObjectivesConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var progress = 1f;
        var count = 0;
        foreach (var objective in args.Mind.Objectives)
        {
            if (objective == ent.Owner || HasComp<AchieveObjectivesConditionComponent>(objective))
                continue;
            progress = Math.Min(progress, _objectives.GetProgress(objective, (args.MindId, args.Mind)) ?? 0);
            count++;
        }
        args.Progress = count == 0 ? 0 : progress;
    }
}
