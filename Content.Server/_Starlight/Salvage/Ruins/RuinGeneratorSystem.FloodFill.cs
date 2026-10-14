using System.Linq;

namespace Content.Server._Starlight.Salvage.Ruins;

public sealed partial class RuinGeneratorSystem
{
    #region Fields

    private static readonly Vector2i[] CardinalOffsets =
    [
        Vector2i.Up,
        Vector2i.Down,
        Vector2i.Left,
        Vector2i.Right,
    ];

    #endregion

    #region Methods

    private Vector2i? FindValidStartLocation(
        CachedMapData cachedData,
        System.Random rand,
        int maxRetries,
        int spaceCost = 99)
    {
        var positions = cachedData.ValidStartPositions;
        if (positions.Count == 0)
            return null;

        for (var i = 0; i < maxRetries; i++)
        {
            var pos = positions[rand.Next(positions.Count)];
            if (cachedData.CostMap[pos] < spaceCost)
                return pos;
        }

        foreach (var pos in positions)
        {
            if (cachedData.CostMap[pos] < spaceCost)
                return pos;
        }

        return null;
    }

    /// <summary>
    /// Multi-stage cost-based flood-fill for irregular branching ruin shapes.
    /// Uses a PriorityQueue so expansion stays cheap as stage budgets grow.
    /// </summary>
    private HashSet<Vector2i> FloodFillMultiStage(
        Dictionary<Vector2i, int> costMap,
        Vector2i start,
        int stagesCount,
        int budgetPerStage,
        HashSet<Vector2i> wallPositions,
        System.Random rand,
        int spaceCost,
        int defaultTileCost)
    {
        var result = new HashSet<Vector2i>();
        var visited = new HashSet<Vector2i>();
        var currentStart = start;

        for (var stage = 0; stage < stagesCount; stage++)
        {
            var stageResult = new HashSet<Vector2i>();
            var stageVisited = new HashSet<Vector2i>();
            var queue = new PriorityQueue<Vector2i, int>();
            var accumulatedCosts = new Dictionary<Vector2i, int>();

            queue.Enqueue(currentStart, 0);
            accumulatedCosts[currentStart] = 0;

            while (queue.TryDequeue(out var current, out var accumulatedCost))
            {
                if (stageVisited.Contains(current))
                    continue;

                stageVisited.Add(current);

                if (!costMap.ContainsKey(current))
                    continue;

                // Each stage needs an anchor even when its selected start is a high-cost wall or window.
                // accumulatedCost already includes this tile's own cost from enqueue.
                var totalCostIfAdded = current == currentStart
                    ? 0
                    : accumulatedCost;
                if (totalCostIfAdded > budgetPerStage)
                    continue;

                stageResult.Add(current);
                result.Add(current);
                visited.Add(current);

                for (var i = 0; i < CardinalOffsets.Length; i++)
                {
                    var neighbor = current + CardinalOffsets[i];
                    if (stageVisited.Contains(neighbor) || visited.Contains(neighbor))
                        continue;

                    if (!costMap.TryGetValue(neighbor, out var neighborCost))
                        continue;

                    var newAccumulatedCost = accumulatedCost + neighborCost;
                    if (newAccumulatedCost > budgetPerStage)
                        continue;

                    if (!accumulatedCosts.TryGetValue(neighbor, out var existingCost) ||
                        newAccumulatedCost < existingCost)
                    {
                        accumulatedCosts[neighbor] = newAccumulatedCost;
                        queue.Enqueue(neighbor, newAccumulatedCost);
                    }
                }
            }

            // Pull in walls adjacent to visited floors so room shells stay coherent.
            var adjacentWithWalls = new HashSet<Vector2i>();
            foreach (var pos in stageResult)
            {
                for (var i = 0; i < CardinalOffsets.Length; i++)
                {
                    var neighbor = pos + CardinalOffsets[i];
                    if (result.Contains(neighbor))
                        continue;

                    if (wallPositions.Contains(neighbor))
                        adjacentWithWalls.Add(neighbor);
                }
            }

            foreach (var wallPos in adjacentWithWalls)
            {
                result.Add(wallPos);
                visited.Add(wallPos);
            }

            if (stage >= stagesCount - 1)
                continue;

            if (!TryPickNextStageStart(result, visited, costMap, rand, spaceCost, defaultTileCost, out currentStart))
                break;
        }

        return result;
    }

    private static bool TryPickNextStageStart(
        HashSet<Vector2i> result,
        HashSet<Vector2i> visited,
        Dictionary<Vector2i, int> costMap,
        System.Random rand,
        int spaceCost,
        int defaultTileCost,
        out Vector2i nextStart)
    {
        var lowCostTiles = new HashSet<Vector2i>();
        var mediumCostTiles = new HashSet<Vector2i>();
        var highCostTiles = new HashSet<Vector2i>();

        foreach (var pos in result)
        {
            for (var i = 0; i < CardinalOffsets.Length; i++)
            {
                var neighbor = pos + CardinalOffsets[i];
                if (visited.Contains(neighbor) || result.Contains(neighbor))
                    continue;

                if (!costMap.TryGetValue(neighbor, out var neighborCost) || neighborCost >= spaceCost)
                    continue;

                if (neighborCost <= defaultTileCost)
                    lowCostTiles.Add(neighbor);
                else if (neighborCost <= 5)
                    mediumCostTiles.Add(neighbor);
                else
                    highCostTiles.Add(neighbor);
            }
        }

        if (TryPickFromSet(lowCostTiles, rand, out nextStart) ||
            TryPickFromSet(mediumCostTiles, rand, out nextStart) ||
            TryPickFromSet(highCostTiles, rand, out nextStart))
        {
            return true;
        }

        nextStart = default;
        return false;
    }

    private static bool TryPickFromSet(HashSet<Vector2i> set, System.Random rand, out Vector2i picked)
    {
        if (set.Count == 0)
        {
            picked = default;
            return false;
        }

        var index = rand.Next(set.Count);
        var i = 0;
        foreach (var pos in set)
        {
            if (i == index)
            {
                picked = pos;
                return true;
            }

            i++;
        }

        picked = default;
        return false;
    }

    #endregion
}
