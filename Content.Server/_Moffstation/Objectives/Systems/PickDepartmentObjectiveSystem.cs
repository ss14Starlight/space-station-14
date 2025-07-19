using System.Linq;
using Content.Server._Moffstation.Objectives.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Moffstation.Objectives.Systems;

/// <summary>
/// Handles assinging a target to an objective entity with <see cref="LocationObjectiveComponent"/> using different components.
/// </summary>
public sealed class PickDepartmentObjectiveSystem : EntitySystem
{
    [Dependency] private readonly LocationObjectiveSystem _target = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PickDepartmentObjectiveComponent, ObjectiveAssignedEvent>(OnRandomDepartmentAssigned);

    }

    private void OnRandomDepartmentAssigned(Entity<PickDepartmentObjectiveComponent> ent, ref ObjectiveAssignedEvent args)
    {
        // invalid objective prototype
        if (!TryComp<LocationObjectiveComponent>(ent.Owner, out var target))
        {
            args.Cancelled = true;
            return;
        }

        // target already assigned
        if (target.Target != null)
            return;


        var departments = new List<DepartmentPrototype>();
        // generate the list of valid departments that can be selected
        foreach (var department in _prototypeManager.EnumeratePrototypes<DepartmentPrototype>())     // Remove invalid departments
        {
            if (!ent.Comp.AllowNonPrimary && !department.Primary)
                continue;
            if (!ent.Comp.AllowHidden && department.EditorHidden)
                continue;
            if (ent.Comp.DepartmentBlacklist.Contains(department.ID))
                continue;
            if (ent.Comp.DepartmentWhitelist.Count != 0 && !ent.Comp.DepartmentWhitelist.Contains(department.ID))
                continue;

            if (!ent.Comp.AllowSameDepartment &&
                _jobs.MindTryGetJob(args.MindId, out var job) &&
                _jobs.TryGetDepartment(job.ID, out var workedDepartment) &&
                workedDepartment.ID == department.ID)
                continue;

            departments.Add(department);
        }

        if (departments.Count == 0)
        {
            args.Cancelled = true;
            return;
        }

        target.Target = _random.Pick(departments.ToList()).Name;
    }
}
