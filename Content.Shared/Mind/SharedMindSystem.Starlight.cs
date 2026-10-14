using Content.Shared._Starlight.Objectives.Targeting;
using Content.Shared.Mind.Filters;
using Content.Shared.Mobs.Components;
using Robust.Shared.Random;

namespace Content.Shared.Mind;

public abstract partial class SharedMindSystem
{
    /// <summary>
    /// Adds living non-humanoid minds explicitly eligible for antagonist objectives.
    /// </summary>
    public void AddObjectiveTargetNonHumanoids(HashSet<Entity<MindComponent>> minds, EntityUid? exclude = null)
    {
        var query = EntityQueryEnumerator<AntagTargetComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState))
        {
            if (!TryGetMind(uid, out var mind, out var mindComp) || mind == exclude || !_mobState.IsAlive(uid, mobState))
                continue;

            minds.Add((mind, mindComp));
        }
    }

    private Entity<MindComponent> PickObjectiveTarget(IMindPool pool, HashSet<Entity<MindComponent>> candidates)
    {
        if (pool is not (HighValueTargetsPool or WeightedAlivePool))
            return _random.Pick(candidates);

        var weighted = new List<Entity<MindComponent>>();
        foreach (var candidate in candidates)
        {
            var weight = pool is WeightedAlivePool normal
                ? normal.Weight(candidate, EntityManager) : HighValueTargetsPool.Weight(candidate, EntityManager);
            for (var i = 0; i < weight; i++)
                weighted.Add(candidate);
        }
        return _random.Pick(weighted);
    }
}
