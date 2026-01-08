using Content.Shared._Starlight.Combat.Effects.EntitySystems;
using Robust.Shared.Map;

namespace Content.Client._Starlight.Combat.Effects.EntitySystems;

/// <summary>
/// Client-side implementation of the armor spark effect system.
/// </summary>
public sealed class ArmorSparkEffectSystem : SharedArmorSparkEffectSystem
{
    protected override EntityUid? SpawnSparkEffectAt(EntityCoordinates coordinates, string effectPrototype)
    {
        // Client doesn't spawn effects directly - handled by server
        return null;
    }

    protected override void PlayRicochetSound(EntityCoordinates coordinates, string soundCollection)
    {
        // Client doesn't play sounds directly - handled by server
    }
}
