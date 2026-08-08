using Content.Shared._Starlight.Scent.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Scent.Components;

// A short-lived, invisible-to-normal-vision object left behind by a ScentComponent entity.
// TimedDespawnComponent handles despawn. See scent_marker.yml.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), Access(typeof(SharedScentSystem))]
public sealed partial class ScentMarkerComponent : Component
{
    [DataField, AutoNetworkedField]
    public string ScentId = string.Empty;

    // Absolute despawn timestamp. Lets the fade animation recompute remaining time correctly
    // whenever it restarts.
    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;

    // How pooled this marker is, 0-1. Maps to alpha/scale client-side.
    [DataField, AutoNetworkedField]
    public float Strength;

    // The decay time actually used for this marker's current life. ExpiresAt - TotalDuration
    // gives the moment this life cycle started, used to compute elapsed fraction client-side.
    [DataField, AutoNetworkedField]
    public TimeSpan TotalDuration;

    // The airtight container the emitter was inside at the moment of this emission, if any.
    [DataField, AutoNetworkedField]
    public EntityUid? ContainedIn;
}
