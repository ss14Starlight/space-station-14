using System.Collections.Frozen;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
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

    private FrozenDictionary<ProtoId<ExtractReactionPrototype>, ExtractReactionPrototype> _typeToExtractReaction = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeExtractComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<SlimeExtractComponent, EntityPausedEvent>(OnPaused);
        SubscribeLocalEvent<SlimeExtractComponent, EntityUnpausedEvent>(OnUnpaused);

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(HandlePrototypesReloaded);
        LoadPrototypes();
    }

    /// <inheritdoc />
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<SlimeExtractComponent>();

        List<ApplyEffectRecord> applyEffectRecords = new();
        List<DeleteRecord> deleteRecords = new();

        while (query.MoveNext(out var uid, out var slimeExtractComponent))
        {
            if (slimeExtractComponent.CurrentlyPaused) continue;
            if (slimeExtractComponent.RemainingUses <= 0) continue;

            if (!_solutionContainerSystem.TryGetSolution(uid, slimeExtractComponent.ContainerName, out var solcom, out var currentSolution)) continue;

            var ToDrop = new List<ProtoId<ExtractReactionPrototype>>();

            foreach ((var reactionId, var activationTime) in slimeExtractComponent.ActivationTimes)
            {
                // Will activate in the future - ignore
                if (activationTime > _gameTiming.CurTime)
                    continue;

                var reaction = _typeToExtractReaction[reactionId];

                if (!IsSolutionRequirementFulfilled(reaction.Requirements, currentSolution))
                {
                    ToDrop.Add(reactionId);
                    continue;
                }

                HandleReaction((uid, slimeExtractComponent), reaction);
            }
            foreach (var reactionId in ToDrop)
            {
                slimeExtractComponent.ActivationTimes.Remove(reactionId);
            }
        }
    }

    private void HandlePrototypesReloaded(PrototypesReloadedEventArgs obj)
    {
        if (obj.WasModified<ExtractReactionPrototype>())
            LoadPrototypes();
    }

    private void LoadPrototypes()
    {
        var dict = new Dictionary<ProtoId<ExtractReactionPrototype>, ExtractReactionPrototype>();
        foreach (var reaction in _prototypeManager.EnumeratePrototypes<ExtractReactionPrototype>())
        {
            if (!dict.TryAdd(reaction.ID, reaction))
                Log.Error($"Found extract reaction with duplicate id {reaction.ID} - all extract reactions must have a unique id, this one will be skipped");
        }

        _typeToExtractReaction = dict.ToFrozenDictionary();
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
        if (entity.Comp.RemainingUses <= 0)
            return;

        foreach (var reactionProtoId in entity.Comp.ExtractReactions)
        {
            var reaction = _typeToExtractReaction[reactionProtoId];
            if (IsSolutionRequirementFulfilled(reaction.Requirements, args.Solution))
            {
                if (reaction.Delay > TimeSpan.Zero)
                {
                    // If we already expect this reaction to go off, don't update the time
                    if (entity.Comp.ActivationTimes.ContainsKey(reactionProtoId))
                        continue;

                    entity.Comp.ActivationTimes[reactionProtoId] = _gameTiming.CurTime + reaction.Delay;
                }
                else
                {
                    // Fire off the reaction immediately
                    HandleReaction(entity, reaction);
                }
            }
            else if (entity.Comp.ActivationTimes.ContainsKey(reactionProtoId))
            {
                entity.Comp.ActivationTimes.Remove(reactionProtoId);
            }
        }
    }

    private void HandleReaction(Entity<SlimeExtractComponent> entity, ExtractReactionPrototype reaction)
    {
        if (!_solutionContainerSystem.TryGetSolution(entity.Owner, entity.Comp.ContainerName, out var solutionComponent, out var currentSolution)) return;

        var minimumScalingFactor = FindMinimumScalingFactor(reaction.Requirements, currentSolution);

        if (reaction.ShouldDelete)
        {
            _entityManager.PredictedQueueDeleteEntity(entity.Owner);
        }
        else
        {
            foreach (var requirement in reaction.Requirements)
            {
                // Remove the reagents directly from the solution, so that we can pass the flag to ignore reagent data
                currentSolution.RemoveReagent(new ReagentId(requirement.Key, null), minimumScalingFactor * requirement.Value, false, true);
            }

            // Since we directly removed the reagents from the solution, we need to call the update function
            // to handle any new chemistry reactions that can occur inside.
            _solutionContainerSystem.UpdateChemicals(solutionComponent.Value);
        }

        foreach (var effect in reaction.Effects)
        {
            // Need to defer the application of effects in order to avoid modifying the query's collection
            // Because the rainbow extract can summon other extracts
            var factor = (minimumScalingFactor * effect.ScalingFactor) + effect.ScalingOffset;
            _entityEffectsSystem.TryApplyEffect(entity.Owner, effect.Effect, factor.Float());
        }

        entity.Comp.RemainingUses -= 1;
    }

    private void OnPaused(Entity<SlimeExtractComponent> entity, ref EntityPausedEvent args)
    {
        entity.Comp.CurrentlyPaused = true;
    }

    private void OnUnpaused(Entity<SlimeExtractComponent> entity, ref EntityUnpausedEvent args)
    {
        entity.Comp.CurrentlyPaused = false;
        foreach ((var reactionId, var ActivationTime) in entity.Comp.ActivationTimes)
        {
            entity.Comp.ActivationTimes[reactionId] = ActivationTime + args.PausedTime;
        }
    }
}

internal record DeleteRecord
{
    public EntityUid _entityUid;

    public DeleteRecord(EntityUid entityUid)
    {
        _entityUid = entityUid;
    }
}

internal record ApplyEffectRecord
{
    public EntityUid _entityUid;
    public EntityEffect _entityEffect;
    public float _scale;

    public ApplyEffectRecord(EntityUid entityUid, EntityEffect entityEffect, float scale)
    {
        _entityUid = entityUid;
        _entityEffect = entityEffect;
        _scale = scale;
    }
}
