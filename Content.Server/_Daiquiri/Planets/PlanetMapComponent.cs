using Content.Shared._Daiquiri.Planets;
using Robust.Shared.Prototypes;

namespace Content.Server._Daiquiri.Planets;

[RegisterComponent]
public sealed partial class PlanetMapComponent : Component
{
    [DataField]
    public ProtoId<PlanetDestinationPrototype> Planet = string.Empty;

    [DataField]
    public int Arrivals = 1;
}