using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Pollen.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class EmitPollenComponent : Component
{
    /// <summary>
    /// The pollen ID emitted by this entity.
    /// If null, the entity's Produce SeedId is used.
    /// </summary>
    [DataField]
    public string? PollenId;

    /// <summary>
    /// Average time between pollen emissions.
    /// </summary>
    [DataField]
    public float EmitInterval = 5f;

    /// <summary>
    /// How long the pollen scent marker remains.
    /// </summary>
    [DataField]
    public float PollenLifetime = 30f;

    /// <summary>
    /// Random variation applied to EmitInterval.
    /// </summary>
    [DataField]
    public float EmitIntervalVariance = 0.3f;

    /// <summary>
    /// Minimum possible emission interval.
    /// </summary>
    [DataField]
    public float MinEmitInterval = 5f;

    /// <summary>
    /// Time when this entity should emit pollen again.
    /// </summary>
    [DataField]
    public TimeSpan NextEmitTime;
}
