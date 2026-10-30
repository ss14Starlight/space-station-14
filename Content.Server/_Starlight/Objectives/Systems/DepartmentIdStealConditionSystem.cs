using System.Linq;
using Content.Server._Starlight.Objectives.Components;
using Content.Shared.Access.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Objectives.Systems;

public sealed partial class DepartmentIdStealConditionSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DepartmentIdStealConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<DepartmentIdStealConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<DepartmentIdStealConditionComponent, ObjectiveGetProgressEvent>(OnProgress);
    }

    private void OnAssigned(Entity<DepartmentIdStealConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        ent.Comp.OwnName = args.Mind.CharacterName;
        var counts = new Dictionary<ProtoId<DepartmentPrototype>, int>();
        var query = EntityQueryEnumerator<IdCardComponent>();
        while (query.MoveNext(out _, out var card))
        {
            if (string.IsNullOrWhiteSpace(card.FullName) || card.FullName == ent.Comp.OwnName)
                continue;
            foreach (var department in card.JobDepartments.Distinct())
                counts[department] = counts.GetValueOrDefault(department) + 1;
        }
        var eligible = counts.Where(pair => pair.Value >= ent.Comp.MinCount &&
            _prototypes.Index(pair.Key) is { Primary: true, EditorHidden: false }).ToList();
        if (eligible.Count == 0 || ent.Comp.MinCount < 1 || ent.Comp.MaxCount < ent.Comp.MinCount)
        {
            args.Cancelled = true;
            return;
        }
        var selected = _random.Pick(eligible);
        ent.Comp.Department = selected.Key;
        ent.Comp.Count = _random.Next(ent.Comp.MinCount, Math.Min(selected.Value, ent.Comp.MaxCount) + 1);
    }

    private void OnAfterAssign(Entity<DepartmentIdStealConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        if (ent.Comp.Department is not { } department)
            return;
        _meta.SetEntityName(ent, Loc.GetString("objective-condition-department-id-title",
            ("count", ent.Comp.Count), ("department", Loc.GetString(_prototypes.Index(department).Name))), args.Meta);
        _meta.SetEntityDescription(ent, Loc.GetString("objective-condition-department-id-description"), args.Meta);
    }

    private void OnProgress(Entity<DepartmentIdStealConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = 0;
        if (ent.Comp.Department is not { } department || ent.Comp.Count < 1 || args.Mind.OwnedEntity is not { } owner)
            return;

        var remaining = new Stack<EntityUid>();
        var visited = new HashSet<EntityUid>();
        remaining.Push(owner);
        var count = 0;
        while (remaining.TryPop(out var uid))
        {
            if (!visited.Add(uid))
                continue;
            if (TryComp<IdCardComponent>(uid, out var card) && !string.IsNullOrWhiteSpace(card.FullName) &&
                card.FullName != ent.Comp.OwnName && card.JobDepartments.Any(id => id == department))
                count++;

            // Include PDAs, pockets and bags, without counting another person's inventory inside a container.
            if ((uid != owner && HasComp<MindContainerComponent>(uid)) ||
                !TryComp<ContainerManagerComponent>(uid, out var containers))
                continue;
            foreach (var container in containers.Containers.Values)
            {
                foreach (var item in container.ContainedEntities)
                    remaining.Push(item);
            }
        }
        args.Progress = Math.Clamp(count / (float)ent.Comp.Count, 0, 1);
    }
}
