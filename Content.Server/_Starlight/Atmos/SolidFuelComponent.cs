using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Atmos;

/// <summary>
/// Material which accumulates heat from nearby ignition sources and is consumed while burning.
/// Times are seconds; ignition time is measured against a smouldering cigarette (rate 1).
/// </summary>
[RegisterComponent]
public sealed partial class SolidFuelComponent : Component
{
    [DataField] public float IgnitionTime = 90f;
    [DataField] public float BurnTime = 60f;
    [DataField] public float CoolingRate = 2f;
    [DataField] public EntProtoId AshPrototype = "Ash";

    [DataField] public float Exposure;
    [DataField] public float BurnedTime;

    /// <summary>Seconds of wetness remaining, independent of stacks added by incendiary weapons.</summary>
    [DataField] public float WetTime;

    /// <summary>Original tile type for transient floor fuel entities; null for ordinary objects.</summary>
    [DataField] public ProtoId<ContentTileDefinition>? TileType;
}
