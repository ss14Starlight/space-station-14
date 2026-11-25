using Content.Shared.Explosion.Components;
using JetBrains.Annotations;

namespace Content.Server.Destructible.Thresholds.Behaviors
{
    /// <summary>
    /// Works like a SpillBehavior combined with an ExplodeBehavior
    /// </summary>
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class SolutionExplosionBehavior : IThresholdBehavior
    {
        [DataField(required: true)]
        public string Solution = default!;

        public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
        {
            if (system.SolutionContainerSystem.TryGetSolution(owner, Solution, out var solutionEnt, out var explodingSolution) // Far Horizons
                && system.EntityManager.TryGetComponent(owner, out ExplosiveComponent? explosiveComponent))
            {
                // Don't explode if there's no solution
                if (explodingSolution.Volume == 0)
                    return;

                // Scale the explosion intensity based on the remaining volume of solution
                var explosionScaleFactor = explodingSolution.FillFraction;

                var spilled = system.SolutionContainerSystem.SplitSolution(solutionEnt.Value, explodingSolution.Volume); // Far Horizons

                // Spill the solution out into the world
                // Spill before exploding in anticipation of a future where the explosion can light the solution on fire.
                var coordinates = system.EntityManager.GetComponent<TransformComponent>(owner).Coordinates;
                system.PuddleSystem.TrySpillAt(coordinates, spilled, out _); // Far Horizons

                // Explode
                // Don't delete the object here - let other processes like physical damage from the
                // explosion clean up the exploding object(s)
                var explosiveTotalIntensity = explosiveComponent.TotalIntensity * explosionScaleFactor;
                system.ExplosionSystem.TriggerExplosive(owner, explosiveComponent, false, explosiveTotalIntensity, user:cause);
            }
        }
    }
}
