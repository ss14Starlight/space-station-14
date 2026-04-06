using System.Linq;
using Content.Server.Objectives.Components;
using Content.Shared._Starlight.Devil;
using Content.Shared.Objectives.Components;

namespace Content.Server._Starlight.Objectives.Systems;

public sealed class DamnConditionSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metadata = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamnConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<DamnConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    public void OnAfterAssign(Entity<DamnConditionComponent> condition, ref ObjectiveAfterAssignEvent args) => _metadata.SetEntityDescription(condition.Owner, Loc.GetString(condition.Comp.DescriptionText, ("amount", condition.Comp.Amount)));

    public void OnGetProgress(Entity<DamnConditionComponent> condition, ref ObjectiveGetProgressEvent args)
    {
        int countedDamnations = 0;
        var damnedQuery = AllEntityQuery<DamnedComponent>();
        while (damnedQuery.MoveNext(out var uid, out var damnedComp))
        {
            if (damnedComp.DamnedBy == args.Mind.OwnedEntity)
            {
                if (condition.Comp.RequireSpecificDamnations)
                {
                    if(damnedComp.Damnations.Except(condition.Comp.RequiredDamnations).Any()) countedDamnations++;
                } else countedDamnations++;
            }
        }

        args.Progress = Math.Clamp(countedDamnations / (float)condition.Comp.Amount, 0, 1);
    }
}
