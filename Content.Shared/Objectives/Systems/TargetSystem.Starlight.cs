using Content.Shared._Starlight.Objectives.Targeting;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;

namespace Content.Shared.Objectives.Systems;

public sealed partial class TargetSystem
{
    /// <summary>
    /// Adds living non-humanoid minds explicitly eligible for antagonist objectives.
    /// </summary>
    public void AddObjectiveTargetNonHumanoids(HashSet<Entity<MindComponent>> minds, EntityUid? exclude = null)
    {
        var query = EntityQueryEnumerator<AntagTargetComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState))
        {
            if (!_mind.TryGetMind(uid, out var mind, out var mindComp) || mind == exclude || !_mobState.IsAlive(uid, mobState))
                continue;

            minds.Add((mind, mindComp));
        }
    }
}
