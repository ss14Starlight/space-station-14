using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.EntityEffects.Effects.Solution;

/// <summary>
/// Modifies a reagent in the metabolites container on the entity's bloodstream component.
/// Positive amounts add the reagent, negative amounts remove it.
/// </summary>
public sealed partial class ModifyReagentFromMetabolitesEntityEffectSystem : EntityEffectSystem<BloodstreamComponent, ModifyReagentFromMetabolites>
{
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;

    protected override void Effect(Entity<BloodstreamComponent> entity, ref EntityEffectEvent<ModifyReagentFromMetabolites> args)
    {
        var amount = args.Effect.Amount * args.Scale;
        var reagent = args.Effect.Reagent;

        // Reject zero amounts - no operation should be performed
        if (amount == 0)
            return;

        // Ensure the metabolites solution is resolved/created
        if (!_solutionContainer.ResolveSolution(entity.Owner, entity.Comp.MetabolitesSolutionName, ref entity.Comp.MetabolitesSolution, out _))
            return;

        var metabolitesSolution = entity.Comp.MetabolitesSolution;
        if (!metabolitesSolution.HasValue)
            return;

        var solution = metabolitesSolution.Value;

        if (amount < 0)
            _solutionContainer.RemoveReagent(solution, reagent, -amount);
        else
            _solutionContainer.TryAddReagent(solution, reagent, amount);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class ModifyReagentFromMetabolites : EntityEffectBase<ModifyReagentFromMetabolites>
{
    /// <summary>
    ///     The reagent ID to add or remove.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    /// <summary>
    ///     The amount of reagent to modify. Positive values add the reagent, negative values remove it.
    /// </summary>
    [DataField(required: true)]
    public FixedPoint2 Amount;

    /// <inheritdoc />
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys, ILocalizationManager loc) =>
        prototype.Resolve(Reagent, out ReagentPrototype? proto)
            ? loc.GetString("entity-effect-guidebook-modify-reagent-from-metabolites",
                ("chance", Probability),
                ("deltasign", MathF.Sign(Amount.Float())),
                ("reagent", proto.LocalizedName),
                ("amount", MathF.Abs(Amount.Float())))
            : null;
}