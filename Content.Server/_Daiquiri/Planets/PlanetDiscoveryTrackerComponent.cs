using Content.Shared._Daiquiri.Planets;
using Robust.Shared.Prototypes;

namespace Content.Server._Daiquiri.Planets;

[RegisterComponent]
public sealed partial class PlanetDiscoveryTrackerComponent : Component
{
    public readonly HashSet<ProtoId<PlanetDestinationPrototype>> Discovered = new();
}