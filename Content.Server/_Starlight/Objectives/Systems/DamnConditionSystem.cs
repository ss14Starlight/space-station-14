using System.Linq;
using Content.Server._Starlight.Devil;
using Content.Server.Objectives.Components;
using Content.Shared._Starlight.Devil;
using Content.Shared.Objectives.Components;

namespace Content.Server._Starlight.Objectives.Systems;

public sealed class DamnConditionSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly DevilSystem _devil = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<DamnConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<DamnConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    public void OnAfterAssign(Entity<DamnConditionComponent> condition, ref ObjectiveAfterAssignEvent args)
    {
        _metadata.SetEntityDescription(condition.Owner, Loc.GetString(condition.Comp.DescriptionText, ("amount", condition.Comp.Amount)));
    }

    public void OnGetProgress(Entity<DamnConditionComponent> condition, ref ObjectiveGetProgressEvent args)
    {
        if(args.Mind.OwnedEntity == null)
        {
            args.Progress = 0;
            return;
        }

        int countedDamnations = _devil.GetSoulsDamned((EntityUid)args.Mind.OwnedEntity, condition.Comp.RequiredDamnations);
        args.Progress = Math.Clamp(countedDamnations / (float)condition.Comp.Amount, 0, 1);
    }
}