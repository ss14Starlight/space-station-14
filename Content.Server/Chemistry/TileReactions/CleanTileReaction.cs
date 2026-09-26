using Content.Shared._Funkystation.Footprints;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Chemistry.TileReactions;

/// <summary>
/// Turns all of the reagents on a puddle into water.
/// </summary>
[DataDefinition]
public sealed partial class CleanTileReaction : ITileReaction
{
    /// <summary>
    /// How much it costs to clean 1 unit of reagent.
    /// </summary>
    /// <remarks>
    /// In terms of space cleaner can clean 1 average puddle per 5 units.
    /// </remarks>
    [DataField("cleanCost")]
    public float CleanAmountMultiplier { get; private set; } = 0.25f;

    /// <summary>
    /// What reagent to replace the tile contents with.
    /// </summary>
    [DataField("reagent")]
    public ProtoId<ReagentPrototype> ReplacementReagent = "Water";

    FixedPoint2 ITileReaction.TileReact(TileRef tile,
        ReagentPrototype reagent,
        FixedPoint2 reactVolume,
        IEntityManager entityManager
        , List<ReagentData>? data)
    {
        #region Starlight
        if (!entityManager.TryGetComponent<MapGridComponent>(tile.GridUid, out var grid))
            return FixedPoint2.Zero;

        var mapSystem = entityManager.System<SharedMapSystem>();
        var entities = mapSystem.GetAnchoredEntities(tile.GridUid, grid, tile.GridIndices);
        #endregion
        var puddleQuery = entityManager.GetEntityQuery<PuddleComponent>(); // Starlight
        var footprintQuery = entityManager.GetEntityQuery<FootprintComponent>();
        var solutionContainerSystem = entityManager.System<SharedSolutionContainerSystem>();
        // Multiply as the amount we can actually purge is higher than the react amount.
        var purgeAmount = reactVolume / CleanAmountMultiplier;

        #region Starlight
        // Lot of new footprint logic here
        while (entities.MoveNext(out var entity))
        {
            var uid = entity.Value;
            Entity<SolutionComponent>? floorSolution = null;
            if (puddleQuery.TryGetComponent(uid, out var puddle))
            {
                solutionContainerSystem.TryGetSolution(uid,
                    puddle.SolutionName,
                    out floorSolution,
                    out _);
            }
            else if (footprintQuery.HasComponent(uid))
            {
                solutionContainerSystem.TryGetSolution(uid,
                    FootprintComponent.SolutionName,
                    out floorSolution,
                    out _);
            }

            if (floorSolution is not { } solution)
                continue;

            var purgeable = solutionContainerSystem.SplitSolutionWithout(solution,
                purgeAmount,
                ReplacementReagent,
                reagent.ID);

            purgeAmount -= purgeable.Volume;

            solutionContainerSystem.TryAddSolution(solution, new Solution(ReplacementReagent, purgeable.Volume));
            #endregion

            if (purgeable.Volume <= FixedPoint2.Zero)
                break;
        }

        return ((reactVolume / CleanAmountMultiplier) - purgeAmount) * CleanAmountMultiplier;
    }
}
