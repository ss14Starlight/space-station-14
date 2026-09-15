using Content.Shared._Starlight.Scent.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Scent.Components;

/// <summary>
/// Marks an entity as having a unique, personal scent, same as DnaComponent for DNA.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedScentSystem))]
public sealed partial class ScentComponent : Component
{
    /// <summary>
    /// This entity's unique scent signature. Null until MapInit assigns one.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? ScentId;

    // How close a new emission needs to be to LastMarkerEntity to merge into it instead of
    // spawning fresh. ~1 tile by default.
    [DataField]
    public float MergeRadius = 1f;

    // Average seconds between emissions, jittered by EmitIntervalVariance.
    [DataField]
    public float EmitInterval = 1.5f;

    [DataField]
    public float EmitIntervalVariance = 0.3f;

    // Floor for the jittered EmitInterval, so a high EmitIntervalVariance can't roll a
    // near-zero or negative delay.
    [DataField]
    public float MinEmitInterval = 0.1f;

    // How much ScentMarkerComponent.Strength increases per merge, capped at 1.
    [DataField]
    public float MergeStrengthStep = 0.25f;

    // TimeSpan.Zero means "not yet rolled" (e.g. right after spawn).
    [DataField]
    public TimeSpan NextEmitTime;

    [DataField]
    public float DecayTime = 30f;

    // Floor for DecayTime after pressure scaling, kept below the average EmitInterval so a
    // vacuum marker actually expires between emissions.
    [DataField]
    public float MinDecayTime = 1f;

    // Current tail of this entity's scent chain. A new emission only merges into this marker.
    [DataField]
    public EntityUid? LastMarkerEntity;
}
