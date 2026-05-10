using Content.Server._Starlight.Objectives.Components;
using Content.Server._Starlight.Objectives.Events;
using Content.Server._Starlight.Railroading;
using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Railroading.Events;
using Content.Shared.Objectives;
using Content.Shared.Objectives.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Objectives.Systems;

public sealed class DepartmentObjectiveSystem : EntitySystem
{

    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly RailroadingSystem _railroad = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DepartmentObjectiveComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<DepartmentObjectiveComponent, RailroadingCardChosenEvent>(OnRailroadingChosen);
        SubscribeLocalEvent<DepartmentObjectiveComponent, CollectObjectiveInfoEvent>(OnCollectObjectiveInfo);
        SubscribeLocalEvent<DepartmentObjectiveComponent, RailroadingCardCompletionQueryEvent>((ent, ref args) => args.IsCompleted = true);
    }

    private void OnCollectObjectiveInfo(Entity<DepartmentObjectiveComponent> ent, ref CollectObjectiveInfoEvent args)
    {
        if (!HasComp<RailroadCardComponent>(ent.Owner))
            return;

        args.Objectives.Add(new ObjectiveInfo
        {
            Title = Loc.GetString(ent.Comp.Title),
            Icon = ent.Comp.Icon,
            Progress = 1.0f,
        });
    }

    private void OnAfterAssign(Entity<DepartmentObjectiveComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        if (ent.Comp.TargetDepartment is not { } target)
            return;

        var departmentName = Loc.GetString(_protoMan.Index(target).Name);
        _meta.SetEntityName(ent, Loc.GetString(ent.Comp.Title, ("department", departmentName)), args.Meta);
    }

    private void OnRailroadingChosen(EntityUid uid, DepartmentObjectiveComponent comp, ref RailroadingCardChosenEvent args)
    {
        if (!TryComp<RailroadableComponent>(args.Subject, out var railroadable)
            || railroadable.ActiveCard is null)
            return;

        if (comp.TargetDepartment is not { } target)
            return;

        var departmentName = Loc.GetString(_protoMan.Index(target).Name);
        comp.Title = Loc.GetString(comp.Title, ("department", departmentName));

        _railroad.InvalidateProgress((args.Subject, railroadable));
    }
}
