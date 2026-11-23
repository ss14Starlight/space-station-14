using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.MeteorShield;

[Serializable, NetSerializable]
public enum MeteorShieldConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum MeteorShieldSatelliteVisuals : byte
{
    Active,
}

[Serializable, NetSerializable]
public sealed class MeteorShieldConsoleUiState : BoundUserInterfaceState
{
    public List<MeteorShieldSatelliteState> Satellites = [];

    /// <summary>
    /// Coverage in percentage.
    /// </summary>
    public float Coverage;

    /// <summary>
    /// Amount of destroyed meteors.
    /// </summary>
    public int Destroyed;

    /// <summary>
    /// Coverage the active station goal asks for, from 0 to 1. Can be null if there's no goal.
    /// </summary>
    public float? GoalCoverage;
}

[Serializable, NetSerializable]
public sealed class MeteorShieldSatelliteState
{
    public NetEntity Entity;
    public string Designation = string.Empty;
    public bool Active;
    public bool InSpace;

    /// <summary>
    /// Distance to the station centre in tiles.
    /// </summary>
    public float Distance;
}

[Serializable, NetSerializable]
public sealed class MeteorShieldToggleMessage(NetEntity satellite) : BoundUserInterfaceMessage
{
    public readonly NetEntity Satellite = satellite;
}
