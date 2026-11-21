using Content.Shared._Starlight.TransitTube;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.RCD.Systems;

public sealed partial class RCDSystem
{
    /// <summary>
    /// The level a transit tube recipe builds on, or null if it builds no transit tube.
    /// </summary>
    private TransitTubeLevel? GetTransitTubeLevel(string? prototype)
    {
        if (prototype == null
            || !_protoManager.TryIndex<EntityPrototype>(prototype, out var proto)
            || !proto.TryGetComponent<TransitTubeComponent>(out var tube, _entityManager.ComponentFactory))
            return null;

        return tube.Level;
    }

    /// <summary>
    /// Whether a transit tube already runs on this level of the tile. Tubes under and over the floor are not
    /// dense, so only this keeps two of them off one tile, while still letting levels cross each other.
    /// </summary>
    private bool TransitTubeLevelTaken(EntityUid ent, TransitTubeLevel? level)
        => level is { } wanted
        && TryComp<TransitTubeComponent>(ent, out var tube)
        && tube.Level == wanted
        && Transform(ent).Anchored;
}
