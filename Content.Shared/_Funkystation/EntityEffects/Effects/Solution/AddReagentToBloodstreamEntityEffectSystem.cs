using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.Solution;

public sealed partial class AddReagentToBloodstreamSystem : EntityEffectSystem<BloodstreamComponent, AddReagentToBloodstream>
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    protected override void Effect(Entity<BloodstreamComponent> entity, ref EntityEffectEvent<AddReagentToBloodstream> args)
    {
        var quantity = args.Effect.Amount * args.Scale;

        Entity<SolutionComponent>? solnEnt = null;

        if (_solutionContainer.ResolveSolution(entity.Owner, entity.Comp.BloodSolutionName, ref solnEnt, out _))
            _solutionContainer.TryAddReagent(solnEnt.Value, args.Effect.Reagent, quantity);
    }
}

public sealed partial class AddReagentToBloodstream : EntityEffectBase<AddReagentToBloodstream>
{
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    [DataField(required: true)]
    public FixedPoint2 Amount;
}
