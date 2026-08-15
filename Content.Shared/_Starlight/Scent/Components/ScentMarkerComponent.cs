using Content.Shared._Starlight.Scent.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Scent.Components;

/// <summary>
/// A short-lived, invisible-to-normal-vision object left behind by a ScentComponent entity.
/// TimedDespawnComponent handles despawn. See scent_marker.yml.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), Access(typeof(SharedScentSystem))]
public sealed partial class ScentMarkerComponent : Component
{
    [DataField, AutoNetworkedField]
    public string ScentId = string.Empty;

    /// <summary>
    /// Absolute despawn timestamp. Lets the fade animation recompute remaining time correctly
    /// whenever it restarts.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;

    /// <summary>
    /// How pooled this marker is, 0-1. Maps to alpha/scale client-side.
    /// </summary>
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Strength;

    /// <summary>
    /// The decay time actually used for this marker's current life. ExpiresAt - TotalDuration
    /// gives the moment this life cycle started, used to compute elapsed fraction client-side.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan TotalDuration;

    /// <summary>
    /// The airtight container the emitter was inside at the moment of this emission, if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ContainedIn;

    /// <summary>
    /// Whether the emitter was dead when this marker was emitted or last refreshed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool WasDead;
    /// <summary>
    /// Whether the emitter was cloaked (StealthComponent, hidden past its ExamineThreshold) at
    /// the moment of this emission.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool WasCloaked;
}
