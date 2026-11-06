using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Localizations;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.Damage;

/// <summary>
/// Adjust the damages on this entity by specified amounts.
/// Amounts are modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class HealthChangeEntityEffectSystem : EntityEffectSystem<DamageableComponent, HealthChange>
{
    [Dependency] private DamageableSystem _damageable = default!;

    protected override void Effect(Entity<DamageableComponent> entity, ref EntityEffectEvent<HealthChange> args)
    {
        var damageSpec = new DamageSpecifier(args.Effect.Damage);

        damageSpec *= args.Scale;

        _damageable.TryChangeDamage(
                entity.AsNullable(),
                damageSpec,
                args.Effect.IgnoreResistances,
                interruptsDoAfters: false);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class HealthChange : EntityEffectBase<HealthChange>
{
    /// <summary>
    /// Damage to apply every cycle. Damage Ignores resistances.
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier Damage = default!;

    [DataField]
    public bool IgnoreResistances = true;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys, ILocalizationManager loc) // Starlight
    {
        var damages = new List<string>();
        var heals = false;
        var deals = false;

        var damageSpec = Damage.ResolveGroups(prototype); // Starlight

        var universalReagentDamageModifier = entSys.GetEntitySystem<DamageableSystem>().UniversalReagentDamageModifier;
        var universalReagentHealModifier = entSys.GetEntitySystem<DamageableSystem>().UniversalReagentHealModifier;

        damageSpec = entSys.GetEntitySystem<DamageableSystem>().ApplyUniversalAllModifiers(damageSpec);

        foreach (var (kind, amount) in damageSpec.DamageDict)
        {
            var sign = FixedPoint2.Sign(amount);
            float mod;

            switch (sign)
            {
                case < 0:
                    heals = true;
                    mod = universalReagentHealModifier;
                    break;
                case > 0:
                    deals = true;
                    mod = universalReagentDamageModifier;
                    break;
                default:
                    continue; // Don't need to show damage types of 0...
            }

            damages.Add(
                loc.GetString("health-change-display",
                    ("kind", prototype.Index<DamageTypePrototype>(kind).LocalizedName),
                    ("amount", MathF.Abs(amount.Float() * mod)),
                    ("deltasign", sign)
                ));
        }

        #region Starlight
        if (damageSpec.MixMax != null && damageSpec.MixMax.Value < FixedPoint2.Zero)
        {
            heals = true;
            var targets = new List<string>();

            foreach (var group in damageSpec.MixMax.Groups)
            {
                targets.Add(prototype.Index<DamageGroupPrototype>(group).LocalizedName);
            }

            foreach (var type in damageSpec.MixMax.Types)
            {
                targets.Add(prototype.Index<DamageTypePrototype>(type).LocalizedName);
            }

            damages.Add(loc.GetString("health-change-mixmax-display",
                ("amount", -damageSpec.MixMax.Value.Float() * universalReagentHealModifier),
                ("targets", ContentLocalizationManager.FormatList(targets))));
        }
        #endregion

        var healsordeals = heals ? (deals ? "both" : "heals") : (deals ? "deals" : "none");

        return loc.GetString("entity-effect-guidebook-health-change",
            ("chance", Probability),
            ("changes", ContentLocalizationManager.FormatList(damages)),
            ("healsordeals", healsordeals));
    }
}
