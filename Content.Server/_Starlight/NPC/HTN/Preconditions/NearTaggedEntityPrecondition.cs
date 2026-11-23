using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content._Starlight.Server.NPC.HTN.Preconditions;

/// <summary>
/// Is any entity with the given tag within range of the owner.
/// </summary>
public sealed partial class NearTaggedEntityPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;
    private SharedTransformSystem _transform = default!;

    [DataField("tag", required: true)]
    public ProtoId<TagPrototype> Tag;

    [DataField("range", required: true)]
    public float Range;

    [DataField]
    public bool Invert;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
    }

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue<EntityCoordinates>(NPCBlackboard.OwnerCoordinates, out var coords, _entManager))
            return false;

        var near = false;
        var query = _entManager.EntityQueryEnumerator<TagComponent, TransformComponent>();

        while (query.MoveNext(out _, out var tags, out var xform))
        {
            if (!tags.Tags.AsReadOnly().Contains(Tag))
                continue;

            if (_transform.InRange(coords, xform.Coordinates, Range))
            {
                near = true;
                break;
            }
        }

        return near ^ Invert;
    }
}