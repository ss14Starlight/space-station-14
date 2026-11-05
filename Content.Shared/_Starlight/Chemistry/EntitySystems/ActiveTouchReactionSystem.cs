using Content.Shared._Starlight.Chemistry.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.StepTrigger.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Chemistry.EntitySystems;

public sealed partial class ActiveTouchReactionSystem: EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ReactiveSystem _reactiveSystem = default!;
    private TimeSpan _nextReaction = TimeSpan.MinValue;
    private static readonly TimeSpan ReactionCooldown = TimeSpan.FromSeconds(2);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime <= _nextReaction) return;

        _nextReaction = _timing.CurTime + ReactionCooldown;
        TickReactions();
    }

    private void TickReactions()
    {
        Dictionary<EntityUid, List<Entity<ActiveTouchReactionComponent, StepTriggerComponent, SolutionComponent>>> reactions = new();

        var reactors = EntityQueryEnumerator<ActiveTouchReactionComponent, StepTriggerComponent, SolutionComponent>();
        while (reactors.MoveNext(out var reactor, out var atr, out var step, out var sol))
        {
            foreach (var dstUid in step.Colliding)
            {
                var reactorsForEntity = reactions.GetOrNew(dstUid, out _);
                reactorsForEntity.Add(new(reactor, atr, step, sol));
            }
        }

        foreach (var reaction in reactions)
        {
            var reactionSolution = new Solution();
            foreach (var reactor in reaction.Value)
            {
                reactionSolution.AddSolution(
                    // This distributes the reaction against all collided reactors
                    reactor.Comp3.Solution.SplitSolution(reactor.Comp1.AmountPerReaction / reaction.Value.Count),
                    null
                );
                Dirty(reactor, reactor.Comp3);
            }
            _reactiveSystem.DoEntityReaction(reaction.Key, reactionSolution, ReactionMethod.Touch);
        }
    }
}