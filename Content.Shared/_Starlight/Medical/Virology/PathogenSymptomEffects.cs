using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Medical.Virology;

public sealed partial class PathogenCappedDamageEffectSystem
    : EntityEffectSystem<DamageableComponent, PathogenCappedDamage>
{
    [Dependency] private DamageableSystem _damageable = default!;

    protected override void Effect(
        Entity<DamageableComponent> entity,
        ref EntityEffectEvent<PathogenCappedDamage> args)
    {
        var current = entity.Comp.Damage.DamageDict.GetValueOrDefault(args.Effect.DamageType.Id).Float();
        var amount = Math.Min(args.Effect.Amount * args.Scale, args.Effect.Maximum - current);
        if (amount <= 0f)
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict[args.Effect.DamageType.Id] = FixedPoint2.New(amount);
        _damageable.TryChangeDamage(
            entity.AsNullable(),
            damage,
            args.Effect.IgnoreResistances,
            interruptsDoAfters: false);
    }
}

public sealed partial class PathogenCappedDamage : EntityEffectBase<PathogenCappedDamage>
{
    [DataField(required: true)]
    public ProtoId<DamageTypePrototype> DamageType;

    [DataField(required: true)]
    public float Amount;

    [DataField(required: true)]
    public float Maximum;

    [DataField]
    public bool IgnoreResistances = true;
}

public sealed partial class PathogenDropActiveItemEffectSystem
    : EntityEffectSystem<HandsComponent, PathogenDropActiveItem>
{
    [Dependency] private SharedHandsSystem _hands = default!;

    protected override void Effect(
        Entity<HandsComponent> entity,
        ref EntityEffectEvent<PathogenDropActiveItem> args)
    {
        _hands.TryDrop(entity.AsNullable(), checkActionBlocker: false);
    }
}

public sealed partial class PathogenDropActiveItem : EntityEffectBase<PathogenDropActiveItem>;
