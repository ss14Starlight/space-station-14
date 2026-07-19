using Content.Shared._Sol.Medical.Virology.Components;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Compatibility API for entity-attached airborne loads.
/// Primary simulation lives in <see cref="GridPathogenAtmosphereSystem"/>.
/// </summary>
public sealed class AirborneContaminantSystem : EntitySystem
{
    [Dependency] private readonly GridPathogenAtmosphereSystem _gridPathogen = default!;

    /// <summary>
    /// Scrubber/sterilizer API: remove pathogen load from an airborne component and its tile.
    /// </summary>
    public float Scrub(EntityUid uid, float amount)
    {
        var removed = 0f;

        if (TryComp<AirborneContaminantComponent>(uid, out var airborne))
        {
            for (var i = airborne.Contaminants.Count - 1; i >= 0; i--)
            {
                var entry = airborne.Contaminants[i];
                var take = Math.Min(entry.Load, amount - removed);
                entry.Load -= take;
                removed += take;
                if (entry.Load <= 0.01f)
                    airborne.Contaminants.RemoveAt(i);
                if (removed >= amount)
                    break;
            }

            Dirty(uid, airborne);
        }

        // Also scrub the tile under the entity.
        removed += _gridPathogen.ScrubAround(uid, amount - removed, 0.6f);
        return removed;
    }
}
