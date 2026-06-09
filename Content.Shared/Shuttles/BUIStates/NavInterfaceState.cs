using System.Numerics; // _Starlight
using Content.Shared.Shuttles.Components; // _Starlight
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class NavInterfaceState
{
    public float MaxRange;

    /// <summary>
    /// The relevant coordinates to base the radar around.
    /// </summary>
    public NetCoordinates? Coordinates;

    /// <summary>
    /// The relevant rotation to rotate the angle around.
    /// </summary>
    public Angle? Angle;

    public bool RotateWithEntity = true;

    /// <summary>
    /// Radar blips for entities that should appear as shaped markers on radar
    /// (e.g. artillery shells, rockets, grenades).
    /// </summary>
    public List<RadarBlipData> Blips = new();

    // _Starlight - transient laser beam traces for hitscan shuttle guns (e.g. Apollo)
    /// <summary>
    /// Transient laser beam traces to draw on radar (for hitscan weapons such as the Apollo).
    /// Each entry represents a fired beam; entries are expired server-side after a short duration.
    /// </summary>
    public List<RadarLaserData> Lasers = new(); // _Starlight

    public NavInterfaceState(
        float maxRange,
        NetCoordinates? coordinates,
        Angle? angle) // Starlight: -docks
    {
        MaxRange = maxRange;
        Coordinates = coordinates;
        Angle = angle;
        // Starlight: -docks
    }
}

/// <summary>
/// A single radar blip entry representing a non-grid entity's position on radar.
/// </summary>
[Serializable, NetSerializable]
public readonly struct RadarBlipData
{
    /// <summary>Map-space coordinates of the blip.</summary>
    public readonly NetCoordinates Coordinates;

    /// <summary>Color of the shape drawn on the radar screen.</summary>
    public readonly Color Color;

    /// <summary>Scale multiplier for the shape size (1.0 = default).</summary>
    public readonly float Scale;

    /// <summary>Which shape to render at this blip's position.</summary>
    public readonly BlipShape Shape; // _Starlight

    public RadarBlipData(NetCoordinates coordinates, Color color, float scale = 1f, BlipShape shape = BlipShape.Triangle) // _Starlight - added shape parameter
    {
        Coordinates = coordinates;
        Color = color;
        Scale = scale;
        Shape = shape;
    }
}

// _Starlight
/// <summary>
/// A transient laser beam drawn as a line on radar.
/// Origin is in entity-relative coordinates; the endpoint is origin + Direction * Length (in map space).
/// </summary>
[Serializable, NetSerializable]
public readonly struct RadarLaserData
{
    /// <summary>Entity-relative coordinates of the beam origin (the firing gun's position).</summary>
    public readonly NetCoordinates Origin;

    /// <summary>Normalized fire direction in map/world space.</summary>
    public readonly Vector2 Direction;

    /// <summary>Beam length in world units.</summary>
    public readonly float Length;

    /// <summary>Color of the laser line.</summary>
    public readonly Color Color;

    public RadarLaserData(NetCoordinates origin, Vector2 direction, float length, Color color)
    {
        Origin = origin;
        Direction = direction;
        Length = length;
        Color = color;
    }
}

[Serializable, NetSerializable]
public enum RadarConsoleUiKey : byte
{
    Key
}
