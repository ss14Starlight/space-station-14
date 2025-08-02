using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.EntityEffects.Effects;

/// <summary>
/// Default metabolism for drink reagents. Attempts to find a ThirstComponent on the target,
/// and to update it's thirst values.
/// </summary>
public sealed partial class ReagentScalingChance : EntityEffect
{
    /// <summary>
    /// what is the minimum required amount before this effect starts scaling
    /// </summary>
    [DataField]
    public FixedPoint2 MinimumAmmount = 200;

    /// <summary>
    /// how much should the % chance scale per u of reagent
    /// </summary>
    [DataField]
    public FixedPoint2 Scaling = 0.2;

    /// <summary>
    /// what is the minimum chance of this effect occuring
    /// </summary>
    [DataField]
    public FixedPoint2 MinimumChance = 0.02;

    /// <summary>
    /// What EntityEffects should be applied upon a successful activation.
    /// </summary>
    [DataField("effects", required: true)]
    public List<EntityEffect> Effects = default!;

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs eera)
            return; //I wanna try to have to figure out where this solution is coming from so deal with it
        var reagent = eera.Reagent;
        var source = eera.Source;
        if (reagent == null || source == null)
            return; //trying to have us scale effects when a container or reagent doesn't exists? impossible
        var amnt = source.GetReagent(new ReagentId(reagent.ID, null)).Quantity;
        if (amnt < MinimumAmmount)
            return; //We dont reach minimum requirements to even try.
        var chance = FixedPoint2.Max((amnt - MinimumAmmount) * Scaling, MinimumChance);
        var random = IoCManager.Resolve<IRobustRandom>();
        if (!random.Prob(Math.Min(1.0f, chance.Float())))
            return; //Failed the dice of fate.

        foreach (var effect in Effects)
        {
            if (!effect.ShouldApply(args, random))
                continue;

            if (effect.ShouldLog)
            {
                var actualEntity = args.TargetEntity;
                var adminLogger = IoCManager.Resolve<ISharedAdminLogManager>();
                adminLogger.Add(
                    LogType.ReagentEffect,
                    effect.LogImpact,
                    $"Metabolism effect {effect.GetType().Name:effect}"
                    + $" of reagent {reagent.LocalizedName:reagent}"
                    + $" applied on entity {actualEntity:entity}"
                    + $" at {args.EntityManager.GetComponent<TransformComponent>(actualEntity).Coordinates:coordinates}"
                );
            }

            effect.Effect(args);
        }
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-reagent-scaling-chance", ("minammount", MinimumAmmount), ("scaling", Scaling * 100), ("minchance", MinimumChance * 100));
    }
}
