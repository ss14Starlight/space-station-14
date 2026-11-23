using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.Server._Starlight.NPC.HTN.Preconditions;

/// <summary>
/// Is the owner's total damage at or above a flat amount.
/// </summary>
public sealed partial class TotalDamagePrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;
    private DamageableSystem _damageable = default!;

    [DataField("damage", required: true)]
    public float Damage;

    [DataField]
    public bool Invert;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _damageable = sysManager.GetEntitySystem<DamageableSystem>();
    }

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<DamageableComponent>(owner, out var damage))
            return false;

        var total = _damageable.GetTotalDamage((owner, damage));

        return (total >= FixedPoint2.New(Damage)) ^ Invert;
    }
}
