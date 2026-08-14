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
        var damageType = args.Effect.DamageType.Id;
        PathogenInfection? infection = null;

        // Only one infection is supported at a time today; if stacked infections are ever
        // added, this needs to select the record belonging to the strain that fired.
        if (TryComp<PathogenInfectionComponent>(entity.Owner, out var infections) &&
            infections.Infections.Count > 0)
        {
            infection = infections.Infections[0];
        }

        // Without an infection record to bill against, the cap falls back to the host's
        // total damage of this type.
        var cappedCurrent = infection?.CappedDamage.GetValueOrDefault(damageType)
            ?? entity.Comp.Damage.DamageDict.GetValueOrDefault(damageType).Float();

        var amount = Math.Min(args.Effect.Amount * args.Scale, args.Effect.Maximum - cappedCurrent);
        if (amount <= 0f)
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict[damageType] = FixedPoint2.New(amount);
        if (!_damageable.TryChangeDamage(
                entity.AsNullable(),
                damage,
                out var applied,
                args.Effect.IgnoreResistances,
                interruptsDoAfters: false))
        {
            return;
        }

        if (infection is null)
            return;

        var appliedAmount = Math.Max(0f, applied.DamageDict.GetValueOrDefault(damageType).Float());
        infection.CappedDamage[damageType] = cappedCurrent + appliedAmount;
    }
}

public sealed partial class PathogenCappedDamage : EntityEffectBase<PathogenCappedDamage>
{
    /// <summary>
    /// Damage type this symptom applies.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<DamageTypePrototype> DamageType;

    /// <summary>
    /// Damage attempted on each symptom expression, before resistance modifiers.
    /// </summary>
    [DataField(required: true)]
    public float Amount;

    /// <summary>
    /// Maximum cumulative damage this effect can add during one infection. When used
    /// without an active infection record, this falls back to the host's current damage.
    /// </summary>
    [DataField(required: true)]
    public float Maximum;

    /// <summary>
    /// Whether the damage bypasses normal resistance modifiers.
    /// </summary>
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
