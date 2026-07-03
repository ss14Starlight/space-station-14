using Content.Shared._Starlight.Genetics.Components;
using Content.Shared._Starlight.Xenobiology;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Genetics.Systems;

public sealed class OnSolutionChangedTraitsSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _entityEffectsSystem = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OnSolutionChangedTraitsComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
    }

    public static bool IsSolutionRequirementFulfilled(Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> requiredSolution, Solution currentSolution) => SlimeExtractSystem.IsSolutionRequirementFulfilled(requiredSolution, currentSolution);

    public static FixedPoint2 FindMinimumScalingFactor(
        Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> requiredSolution, Solution currentSolution) =>
        SlimeExtractSystem.FindMinimumScalingFactor(requiredSolution, currentSolution);

    private void OnSolutionChanged(Entity<OnSolutionChangedTraitsComponent> entity,
        ref SolutionContainerChangedEvent args)
    {
        if (TerminatingOrDeleted(entity.Owner)) return;
        foreach (var (extractReactionProto, initialScale) in entity.Comp.Traits)
        {
            var osctp = _prototypeManager.Index(extractReactionProto);
            var reaction = _prototypeManager.Index(osctp.ExtractReaction);
            if (IsSolutionRequirementFulfilled(reaction.Requirements, args.Solution))
            {
                var scale = FindMinimumScalingFactor(reaction.Requirements, args.Solution);
                foreach (var (id, amt) in reaction.Requirements)
                {
                    args.Solution.RemoveReagent(new ReagentQuantity(new ReagentId(id, null), amt * scale), false, true);
                }

                foreach (var effect in reaction.Effects)
                {
                    var factor = (initialScale * scale * effect.ScalingFactor) + effect.ScalingOffset;
                    _entityEffectsSystem.TryApplyEffect(entity.Owner, effect.Effect, factor.Float());
                }
            }
        }
    }
}
