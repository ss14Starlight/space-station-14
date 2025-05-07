using System.Linq;
using Content.Server._Moffstation.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Moffstation.Objectives.Systems;

/// <summary>
/// Handles assinging a target to an objective entity with <see cref="TargetDepartmentComponent"/> using different components.
/// </summary>
public sealed class PickObjectiveDepartmentSystem : EntitySystem
{
    [Dependency] private readonly TargetDepartmentSystem _target = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PickSpecificDepartmentComponent, ObjectiveAssignedEvent>(OnSpecificDepartmentAssigned);
        SubscribeLocalEvent<PickRandomDepartmentComponent, ObjectiveAssignedEvent>(OnRandomDepartmentAssigned);

    }

    private void OnSpecificDepartmentAssigned(Entity<PickSpecificDepartmentComponent> ent, ref ObjectiveAssignedEvent args)
    {
        // invalid objective prototype
        if (!TryComp<TargetDepartmentComponent>(ent.Owner, out var target))
        {
            args.Cancelled = true;
            return;
        }

        // target already assigned
        if (target.Target != null)
            return;

        if (args.Mind.OwnedEntity == null)
        {
            args.Cancelled = true;
            return;
        }

        var user = args.Mind.OwnedEntity.Value;
        if (!TryComp<TargetDepartmentOverrideComponent>(user, out var targetComp) || targetComp.Target == null)
        {
            args.Cancelled = true;
            return;
        }

        _target.SetTarget(ent.Owner, targetComp.Target);
    }

    private void OnRandomDepartmentAssigned(Entity<PickRandomDepartmentComponent> ent, ref ObjectiveAssignedEvent args)
    {
        // invalid objective prototype
        if (!TryComp<TargetDepartmentComponent>(ent.Owner, out var target))
        {
            args.Cancelled = true;
            return;
        }

        // target already assigned
        if (target.Target != null)
            return;


        var departments = new List<DepartmentPrototype>();
        foreach (var department in _prototypeManager.EnumeratePrototypes<DepartmentPrototype>().ToList())     // Remove invalid departments
        {
            switch (department.ID)
            {
                case "CentralCommand":
                case "Silicon":
                case "Specific":
                    continue;
            }
            departments.Add(department);
        }
        _target.SetTarget(ent.Owner, Loc.GetString(_random.Pick(departments).Name), target);
    }
}
