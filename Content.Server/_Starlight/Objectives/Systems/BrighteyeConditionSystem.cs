using Content.Shared.Objectives.Components;
using Content.Shared.Mind;
using Content.Server._Starlight.Objectives.Components;
using Content.Shared._Starlight.Shadekin;

namespace Content.Server._Starlight.Objectives.Systems;

public sealed class BrighteyeConditionSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BrighteyeSurviveConditionComponent, ObjectiveGetProgressEvent>(OnSurviveGetProgress);
        SubscribeLocalEvent<BrighteyePortalConditionComponent, ObjectiveGetProgressEvent>(OnPortalGetProgress);
    }

    private void OnSurviveGetProgress(EntityUid uid, BrighteyeSurviveConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (args.Mind.OwnedEntity == null || _mind.IsCharacterDeadIc(args.Mind))
        {
            args.Progress = 0f;
            return;
        }

        args.Progress = HasComp<BrighteyeComponent>(args.Mind.OwnedEntity) ? 1f : 0f; 
    }

    private void OnPortalGetProgress(EntityUid uid, BrighteyePortalConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (args.Mind.OwnedEntity == null || !TryComp<BrighteyeComponent>(args.Mind.OwnedEntity, out var brighteye))
        {
            args.Progress = 0f;
            return;
        }

        args.Progress = brighteye.Portal is null ? 0f : 1f; 
    }
}
