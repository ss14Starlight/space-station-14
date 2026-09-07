using Content.Shared.Body.Organ;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.EntityEffects.Effects.Solution;

/// <summary>
/// Adjust a reagent in any solution on the body (bloodstream, metabolites, stomach, etc.).
/// </summary>
public sealed partial class ModifySolutionReagentEntityEffectSystem : EntityEffectSystem<SolutionManagerComponent, ModifySolutionReagent>
{
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    protected override void Effect(Entity<SolutionManagerComponent> entity, ref EntityEffectEvent<ModifySolutionReagent> args)
    {
        if (!TryResolve(entity.Owner, args.Effect.Target, out var target) || target == null)
            return;

        var qty = args.Effect.Amount * args.Scale;
        if (qty > 0)
            _solution.TryAddReagent(target!.Value, args.Effect.Reagent, qty);
        else
            _solution.RemoveReagent(target!.Value, args.Effect.Reagent, -qty);

        _solution.UpdateChemicals(target!.Value);
    }

    private bool TryResolve(EntityUid owner, string id, out Entity<SolutionComponent>? target)
    {
        target = null;
        Content.Shared.Chemistry.Components.Solution? sol = null;

        if (_solution.TryGetSolution(owner, id, out target, out sol))
            return true;

        if (TryComp<OrganComponent>(owner, out var organ) && organ.Body is { } body)
        {
            if (_solution.TryGetSolution(body, id, out target, out sol))
                return true;
            owner = body;
        }

        var query = EntityQueryEnumerator<OrganComponent>();
        while (query.MoveNext(out var organUid, out var organComp))
        {
            if (organComp.Body != owner)
                continue;
            if (_solution.TryGetSolution(organUid, id, out target, out sol))
                return true;
        }

        return false;
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class ModifySolutionReagent : EntityEffectBase<ModifySolutionReagent>
{
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    [DataField(required: true)]
    public FixedPoint2 Amount;

    [DataField(required: true)]
    public string Target = default!;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys, ILocalizationManager loc)
    {
        return prototype.Resolve(Reagent, out ReagentPrototype? proto)
            ? loc.GetString("entity-effect-guidebook-modify-solution-reagent",
                ("chance", Probability),
                ("deltasign", MathF.Sign(Amount.Float())),
                ("reagent", proto.LocalizedName),
                ("amount", MathF.Abs(Amount.Float())),
                ("solution", Target))
            : null;
    }
}
