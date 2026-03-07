using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server.Shuttles.Components;

/// <summary>
/// Placed on a hitscan shuttle gun (e.g. ShuttleGunApollo) to make it emit transient laser
/// beam lines on nearby radar displays when it fires.
/// The system records the gun's fire direction on each shot and expires entries after
/// <see cref="TraceDuration"/> seconds.
/// </summary>
[RegisterComponent]
public sealed partial class RadarLaserTrackerComponent : Component
{
    /// <summary>
    /// Color of the laser line drawn on radar.
    /// </summary>
    [DataField]
    public Color LaserColor = Color.FromHex("#FF44FF");

    /// <summary>
    /// How many seconds the laser trace remains visible on radar after firing.
    /// </summary>
    [DataField]
    public float TraceDuration = 0.6f;

    /// <summary>
    /// Maximum length of the laser line on radar (world units / tiles).
    /// Should match or exceed the effective range of the hitscan weapon.
    /// </summary>
    [DataField]
    public float MaxRange = 35f;

    /// <summary>
    /// Active laser traces: (origin in map space, normalized fire direction, expiry game time).
    /// Populated by <see cref="Content.Server.Shuttles.Systems.RadarLaserSystem"/> on each shot.
    /// </summary>
    [ViewVariables]
    public List<(MapCoordinates Origin, Vector2 Direction, float ExpiryTime)> Traces = new();
}
