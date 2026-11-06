using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server._Starlight.Objectives.Components;
using Content.Shared._Starlight.Medical.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Objectives.Systems;

public sealed partial class StealHeartConditionSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    private static readonly ProtoId<TagPrototype> _heartTag = "Heart";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StealHeartConditionComponent, ObjectiveAssignedEvent>(OnAssigned,
            after: [typeof(PickObjectiveTargetSystem)]);
        SubscribeLocalEvent<StealHeartConditionComponent, ObjectiveGetProgressEvent>(OnProgress);
        SubscribeLocalEvent<TagComponent, OrganRemovedFromBodyEvent>(OnHeartRemoved);
    }

    /// <summary>
    /// Returns installed organs tagged as hearts eligible for theft.
    /// </summary>
    public IEnumerable<EntityUid> GetHearts(EntityUid body)
    {
        foreach (var organ in _body.GetBodyOrgans(body))
        {
            if (_tags.HasTag(organ.Id, _heartTag))
                yield return organ.Id;
        }
    }

    private void OnAssigned(Entity<StealHeartConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<TargetObjectiveComponent>(ent, out var target) ||
            !TryComp<MindComponent>(target.Target, out var mind) || mind.OwnedEntity is not { } body)
        {
            args.Cancelled = true;
            return;
        }

        ent.Comp.Hearts.Clear();
        foreach (var heart in GetHearts(body))
            ent.Comp.Hearts.Add(heart);
        args.Cancelled = ent.Comp.Hearts.Count == 0;
    }

    private void OnHeartRemoved(Entity<TagComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (!_tags.HasTag(ent.Comp, _heartTag) || !_mind.TryGetMind(args.OldBody, out var mindId, out _))
            return;

        // Also recognize hearts removed after a transplant or the target moving into a cloned body.
        var query = EntityQueryEnumerator<StealHeartConditionComponent, TargetObjectiveComponent>();
        while (query.MoveNext(out _, out var condition, out var target))
        {
            if (target.Target == mindId)
                condition.Hearts.Add(ent.Owner);
        }
    }

    private void OnProgress(Entity<StealHeartConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = 0;
        if (args.Mind.OwnedEntity is not { } owner)
            return;

        foreach (var heart in ent.Comp.Hearts)
        {
            if (!Exists(heart) || !_tags.HasTag(heart, _heartTag))
                continue;

            var uid = heart;
            while (uid != owner)
            {
                // no, you can't carry someone to "have" their heart
                if ((uid != heart && HasComp<MindContainerComponent>(uid)) ||
                    !_containers.TryGetContainingContainer((uid, null, null), out var container))
                    break;

                uid = container.Owner;
            }

            if (uid == owner)
            {
                args.Progress = 1;
                return;
            }
        }
    }
}
