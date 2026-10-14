using System.Linq;

namespace Content.Shared._Starlight.Objectives.ObjectivePicker;

/// <summary>
/// Shared validation and completion search for manual, random, and timeout selections.
/// </summary>
public static partial class ObjectivePickerSelection
{
    public const float Tolerance = 0.0001f;

    public static float Difficulty(PotentialObjectivesComponent offers, IEnumerable<NetEntity> selected)
        => selected.Distinct().Sum(id => offers.Difficulties.GetValueOrDefault(id));

    public static bool Available(PotentialObjectivesComponent offers, NetEntity id)
        => offers.ObjectiveOptions.ContainsKey(id) && !offers.UnavailableObjectives.Contains(id);

    public static bool Compatible(PotentialObjectivesComponent offers, NetEntity id, IEnumerable<NetEntity> selected)
            => (!offers.Conflicts.TryGetValue(id, out var conflicts) || !selected.Any(conflicts.Contains)) &&
                TryAssignTargets(offers, selected.Append(id), out _);

    public static bool Valid(PotentialObjectivesComponent offers, IReadOnlyCollection<NetEntity> selected,
        bool ignoreMulligan = false)
    {
        if ((selected.Count == 0 && offers.MinimumDifficulty > Tolerance) ||
            selected.Any(id => !Available(offers, id) || !Compatible(offers, id, selected)))
            return false;
        return Difficulty(offers, selected) + Tolerance >= offers.MinimumDifficulty ||
                CanSelectUnderBudget(offers, ignoreMulligan);
    }

    /// <summary>
    /// A low-budget choice is allowed only when no available selection can meet the budget.
    /// Timeout selection uses the same rule without requiring a spent mulligan.
    /// </summary>
    public static bool CanSelectUnderBudget(PotentialObjectivesComponent offers, bool ignoreMulligan = false)
        => (offers.MulliganUsed || ignoreMulligan) &&
                !TryComplete(offers, offers.ObjectiveOptions.Keys, Array.Empty<NetEntity>(), out _);

    /// <summary>
    /// Finds a compatible completion, without imposing a maximum count or difficulty.
    /// </summary>
    public static bool TryComplete(PotentialObjectivesComponent offers, IEnumerable<NetEntity> order,
        IEnumerable<NetEntity> retained, out HashSet<NetEntity> selected)
        => Search(offers, order, retained, false, out selected);

    /// <summary>
    /// Prefer a full budget, keeping the retained offer when possible. If the budget is globally
    /// impossible and fallback is allowed, take the highest-difficulty compatible selection.
    /// </summary>
    public static bool TrySelect(PotentialObjectivesComponent offers, IEnumerable<NetEntity> order,
        IEnumerable<NetEntity> retained, out HashSet<NetEntity> selected, bool ignoreMulligan = false)
    {
        var candidates = order.ToArray();
        if (TryComplete(offers, candidates, retained, out selected) ||
            TryComplete(offers, candidates, Array.Empty<NetEntity>(), out selected))
            return true;
        if (!CanSelectUnderBudget(offers, ignoreMulligan))
            return false;
        return Search(offers, candidates, Array.Empty<NetEntity>(), true, out selected);
    }

    private static bool Search(PotentialObjectivesComponent offers, IEnumerable<NetEntity> order,
        IEnumerable<NetEntity> retained, bool bestEffort, out HashSet<NetEntity> selected)
    {
        var result = retained.ToHashSet();
        selected = result;
        if (result.Any(id => !Available(offers, id) || !Compatible(offers, id, result)))
            return false;

        var candidates = order.Distinct().Where(id => Available(offers, id) && !result.Contains(id) &&
            (bestEffort || offers.Difficulties.GetValueOrDefault(id) > 0)).ToArray();
        var best = new HashSet<NetEntity>(result);
        var bestDifficulty = Difficulty(offers, result);
        var complete = Visit(0, bestDifficulty);
        if (bestEffort)
        {
            selected = best;
            return best.Count > 0 || offers.MinimumDifficulty <= Tolerance;
        }
        return complete;

        bool Visit(int start, float difficulty)
        {
            if (!bestEffort && difficulty + Tolerance >= offers.MinimumDifficulty)
                return result.Count > 0 || offers.MinimumDifficulty <= Tolerance;
            if (bestEffort && (difficulty > bestDifficulty ||
                    (difficulty == bestDifficulty && result.Count > best.Count)))
            {
                best = new HashSet<NetEntity>(result);
                bestDifficulty = difficulty;
            }

            var possible = difficulty;
            for (var i = start; i < candidates.Length; i++)
            {
                if (Compatible(offers, candidates[i], result))
                    possible += Math.Max(0, offers.Difficulties.GetValueOrDefault(candidates[i]));
            }
            if ((!bestEffort && possible + Tolerance < offers.MinimumDifficulty) ||
                (bestEffort && possible < bestDifficulty))
                return false;

            for (var i = start; i < candidates.Length; i++)
            {
                var id = candidates[i];
                if (!Compatible(offers, id, result))
                    continue;
                result.Add(id);
                if (Visit(i + 1, difficulty + offers.Difficulties.GetValueOrDefault(id)))
                    return true;
                result.Remove(id);
            }
            return false;
        }
    }
    /// <summary>
    /// Finds targets without exposing their identities. Independent objective groups may share a
    /// target; objectives whose target filters conflict need distinct candidates.
    /// </summary>
    public static bool TryAssignTargets(PotentialObjectivesComponent offers, IEnumerable<NetEntity> selected,
        out Dictionary<NetEntity, int> assignments, Func<NetEntity, IEnumerable<int>>? candidateOrder = null)
    {
        var result = new Dictionary<NetEntity, int>();
        assignments = result;
        var remaining = selected.Distinct().Where(offers.TargetPools.ContainsKey).ToHashSet();
        while (remaining.Count > 0)
        {
            var group = new HashSet<NetEntity>();
            Collect(remaining.First());
            var order = group.OrderBy(id => offers.TargetPools[id].Count).ToArray();
            if (order.Any(id => offers.TargetPools[id].Count == 0))
                return false;
            var pools = order.ToDictionary(id => id,
                id => (candidateOrder?.Invoke(id) ?? offers.TargetPools[id]).ToArray());
            if (group.All(id => group.All(other => id == other || Conflicts(id, other))))
            {
                // Most filters form independent cliques (kill, lesson, help). Bipartite matching
                // avoids factorial searches when several offers compete for a small target pool.
                var occupied = new Dictionary<int, NetEntity>();
                foreach (var id in order)
                {
                    if (!Match(id, new HashSet<int>()))
                        return false;
                }

                bool Match(NetEntity id, HashSet<int> visited)
                {
                    foreach (var token in pools[id].OrderBy(occupied.ContainsKey))
                    {
                        if (!visited.Add(token) || (occupied.TryGetValue(token, out var other) && !Match(other, visited)))
                            continue;
                        occupied[token] = id;
                        result[id] = token;
                        return true;
                    }
                    return false;
                }
            }
            else if (!Assign(0))
                return false;

            bool Assign(int index)
            {
                if (index == order.Length)
                    return true;
                var id = order[index];
                foreach (var token in pools[id])
                {
                    if (result.Any(pair => pair.Value == token && Conflicts(id, pair.Key)))
                        continue;
                    result[id] = token;
                    if (Assign(index + 1))
                        return true;
                    result.Remove(id);
                }
                return false;
            }

            void Collect(NetEntity id)
            {
                remaining.Remove(id);
                group.Add(id);
                foreach (var other in remaining.ToArray())
                {
                    if (Conflicts(id, other))
                        Collect(other);
                }
            }
        }
        return true;

        bool Conflicts(NetEntity a, NetEntity b)
            => (offers.TargetConflicts.TryGetValue(a, out var first) && first.Contains(b)) ||
                (offers.TargetConflicts.TryGetValue(b, out var second) && second.Contains(a));
    }
}
