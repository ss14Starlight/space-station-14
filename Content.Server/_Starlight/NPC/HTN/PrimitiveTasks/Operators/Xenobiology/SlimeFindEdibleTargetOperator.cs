using System.Threading;
using System.Threading.Tasks;
using Content.Server._Starlight.Xenobiology;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared._Starlight.Xenobiology;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.NPC.HTN.PrimitiveTasks.Operators.Xenobiology;

public sealed partial class SlimeFindEdibleTargetOperator : HTNOperator
{
    /*
     * This class collects known edible targets for the hive mind.
     * With this, slimes should bunch up around nearby edible targets, and not aimlessly and separately search for targets.
     * This doesn't require pathfinding, so slimes can theoretically smell targets through walls.
     */

    [Dependency] private readonly IEntityManager _entManager = default!;

    private SlimeBrainSystem _slimeBrainSystem = default!;
    private EntityLookupSystem _lookup = default!;
    private TagSystem _tagSystem = default!;

    /// <summary>
    /// The tag an entity must have in order to be considered safe to eat (not desperate).
    /// </summary>
    [DataField(required: true)]
    public ProtoId<TagPrototype> TargetFoodTag = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _slimeBrainSystem = sysManager.GetEntitySystem<SlimeBrainSystem>();
        _lookup = sysManager.GetEntitySystem<EntityLookupSystem>();
        _tagSystem = sysManager.GetEntitySystem<TagSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<SlimeComponent>(owner, out var slime))
            return (false, null);

        foreach (var entity in _lookup.GetEntitiesInRange(owner, _slimeBrainSystem.FoodSearchRange))
        {
            if (_tagSystem.HasTag(entity, TargetFoodTag))
            {
                if (_slimeBrainSystem.TryAddTargetFood(entity))
                    return (true, null);
            }
        }

        _slimeBrainSystem.SlimeUnsuccessfulFoodFind(owner);
        return (false, null);
    }
}
