using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Body.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.EntityConditions;

namespace Content.Shared._Funkystation.EntityConditions.Conditions;

/// <summary>
/// Returns true if the entity's bloodstream solution contains a reagent within the specified min/max range.
/// </summary>
/// <inheritdoc cref="EntityConditionSystem{T, TCondition}"/>
public sealed partial class BloodReagentEntityConditionSystem : EntityConditionSystem<BloodstreamComponent, BloodReagentCondition>
{
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    protected override void Condition(Entity<BloodstreamComponent> entity, ref EntityConditionEvent<BloodReagentCondition> args)
    {
        if (!_solution.ResolveSolution(entity.Owner, entity.Comp.BloodSolutionName, ref entity.Comp.BloodSolution, out var solution))
        {
            args.Result = false;
            return;
        }

        var quantity = solution.GetTotalPrototypeQuantity(args.Condition.Reagent);

        args.Result = quantity >= args.Condition.Min && quantity <= args.Condition.Max;
    }
}

/// <inheritdoc cref="EntityCondition"/>
public sealed partial class BloodReagentCondition : EntityConditionBase<BloodReagentCondition>
{
    [DataField]
    public FixedPoint2 Min = FixedPoint2.Zero;

    [DataField]
    public FixedPoint2 Max = FixedPoint2.MaxValue;

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<ReagentPrototype>), required: true)]
    public string Reagent = string.Empty;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        if (!prototype.TryIndex<ReagentPrototype>(Reagent, out var reagentProto))
            return Loc.GetString("entity-condition-guidebook-unknown-reagent");

        return Loc.GetString("entity-condition-guidebook-blood-reagent-threshold",
            ("reagent", reagentProto.LocalizedName),
            ("min", Min.Float()),
            ("max", Max == FixedPoint2.MaxValue ? (float)int.MaxValue : Max.Float()));
    }
}
