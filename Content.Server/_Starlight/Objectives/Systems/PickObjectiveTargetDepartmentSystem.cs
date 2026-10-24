using System.Linq;
using Content.Server._Starlight.Objectives.Components;
using Content.Shared._Starlight.Railroading.Events;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Objectives.Systems;

public sealed partial class PickObjectiveTargetDepartmentSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PickRandomDepartmentComponent, ObjectiveAssignedEvent>(OnRandomDepartmentAssigned);
        SubscribeLocalEvent<PickRandomDepartmentComponent, RailroadingAssignedEvent>(OnRailroadingAssigned);
    }

    private void OnRailroadingAssigned(Entity<PickRandomDepartmentComponent> ent, ref RailroadingAssignedEvent args)
    {
        _mind.TryGetMind(args.Subject, out var mindId, out _);
        args.Cancelled |= !AssignDepartment(ent, mindId);
    }

    private void OnRandomDepartmentAssigned(Entity<PickRandomDepartmentComponent> ent, ref ObjectiveAssignedEvent args)
    {
        args.Cancelled |= !AssignDepartment(ent, args.MindId);
    }

    private bool AssignDepartment(Entity<PickRandomDepartmentComponent> ent, EntityUid? mindId)
    {
        if (!TryComp<DepartmentObjectiveComponent>(ent.Owner, out var target))
            return false;

        //  target already assigned
        if (target.TargetDepartment != null)
            return true;

        JobPrototype? job = null;
        if (!ent.Comp.AllowSameDepartment)
            _jobs.MindTryGetJob(mindId, out job);

        var depProtos = _protoMan.EnumeratePrototypes<DepartmentPrototype>()
            .Where(d => !ent.Comp.Exclude.Contains(d.ID) &&
                (ent.Comp.Whitelist.Count == 0 || ent.Comp.Whitelist.Contains(d.ID)) &&
                (job == null || !d.Roles.Contains(job.ID)));
        if (ent.Comp.ExcludeNonPrimary)
            depProtos = depProtos.Where(d => d.Primary);
        if (ent.Comp.ExcludeHidden)
            depProtos = depProtos.Where(d => !d.EditorHidden);

        var departments = depProtos.ToList();

        if (departments.Count == 0)
            return false;

        target.TargetDepartment = _random.Pick(departments).ID;
        return true;
    }
}
