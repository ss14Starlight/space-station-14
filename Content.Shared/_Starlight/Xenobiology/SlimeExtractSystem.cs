using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Xenobiology;

/// <summary>
/// Handles the general behavior of slime extracts
/// </summary>
public sealed class SlimeExtractSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _entityEffectsSystem = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeExtractComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<SlimeExtractActiveReactionComponent, EntityPausedEvent>(OnPaused);
        SubscribeLocalEvent<SlimeExtractActiveReactionComponent, EntityUnpausedEvent>(OnUnpaused);
        SubscribeLocalEvent<SlimeExtractComponent, ExaminedEvent>(OnExamined);
    }

    public bool IsSolutionRequirementFulfilled(Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> requiredSolution, Solution currentSolution)
    {
        foreach (var req in requiredSolution)
        {
            var amount = currentSolution.GetTotalPrototypeQuantity(req.Key);
            if (amount < req.Value) return false;
        }

        return true;
    }

    public FixedPoint2 FindMinimumScalingFactor(Dictionary<ProtoId<ReagentPrototype>, FixedPoint2>requiredSolution, Solution currentSolution)
    {
        var minimumScalingFactor = FixedPoint2.MaxValue;
        foreach (var req in requiredSolution)
        {
            var amount = currentSolution.GetTotalPrototypeQuantity(req.Key);
            minimumScalingFactor = FixedPoint2.Min(minimumScalingFactor, amount/req.Value);
        }
        return minimumScalingFactor;
    }

    private void OnSolutionChanged(Entity<SlimeExtractComponent> entity, ref SolutionContainerChangedEvent args)
    {
        if (TerminatingOrDeleted(entity.Owner)) return;
        _entityManager.EnsureComponent<SlimeExtractActiveReactionComponent>(entity.Owner,
            out var activeReactionComponent);
        foreach (var extractReactionProto in entity.Comp.ExtractReactions)
        {
            var reaction = _prototypeManager.Index<ExtractReactionPrototype>(extractReactionProto);
            if (IsSolutionRequirementFulfilled(reaction.Requirements, args.Solution))
            {
                activeReactionComponent.ActiveReactions[extractReactionProto] = _gameTiming.CurTime + reaction.Delay;
            }
            else
            {
                activeReactionComponent.ActiveReactions.Remove(extractReactionProto);
            }
        }

        if (activeReactionComponent.ActiveReactions.Count == 0)
            _entityManager.RemoveComponent(entity.Owner, activeReactionComponent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<SlimeExtractComponent, SlimeExtractActiveReactionComponent>();
        while (query.MoveNext(out var uid, out var slimeExtractComponent, out var activeReactionComponent))
        {
            bool wasActivated = false;
            bool shouldDelete = false;
            foreach (var reactionPair in activeReactionComponent.ActiveReactions)
            {
                if (reactionPair.Value <= _gameTiming.CurTime)
                {
                    if (!_solutionContainerSystem.TryGetSolution(uid, slimeExtractComponent.ContainerName, out var solutionComponent, out var currentSolution)) return;
                    var reaction = _prototypeManager.Index<ExtractReactionPrototype>(reactionPair.Key);
                    if (IsSolutionRequirementFulfilled(reaction.Requirements, currentSolution))
                    {
                        var minimumScalingFactor = FindMinimumScalingFactor(reaction.Requirements, currentSolution);
                        foreach (var requirement in reaction.Requirements)
                        {
                            var reagentToRemove = new ReagentQuantity(new ReagentId(requirement.Key, null),
                                minimumScalingFactor * requirement.Value);
                            currentSolution.RemoveReagent(reagentToRemove, false, true);
                        }
                        foreach (var effect in reaction.Effects)
                        {
                            var factor = (minimumScalingFactor * effect.ScalingFactor) + effect.ScalingOffset;
                            _entityEffectsSystem.TryApplyEffect(uid, effect.Effect, factor.Float());
                        }

                        wasActivated = true;
                        if (reaction.ShouldDelete) shouldDelete = true;
                    }
                }
            }

            if (wasActivated) slimeExtractComponent.RemainingUses -= 1;
            if (shouldDelete && slimeExtractComponent.RemainingUses <= 0)
            {
                PredictedQueueDel(uid);
            }
        }
    }

    private void OnPaused(Entity<SlimeExtractActiveReactionComponent> entity, ref EntityPausedEvent args) => entity.Comp.CurrentlyPaused = true;

    private void OnUnpaused(Entity<SlimeExtractActiveReactionComponent> entity, ref EntityUnpausedEvent args)
    {
        entity.Comp.CurrentlyPaused = false;
        foreach (var activeReaction in entity.Comp.ActiveReactions.Keys)
        {
            entity.Comp.ActiveReactions[activeReaction] += args.PausedTime;
        }
    }

    private void OnExamined(Entity<SlimeExtractComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var str = ent.Comp.RemainingUses <= 0
            ? Loc.GetString("slime-extract-exhausted")
            : Loc.GetString("slime-extract-not-exhausted");

        args.PushMarkup(str);
    }
}
