using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Shuttles.Components;

/// <summary>
/// A console that allows launching a drop pod at a general sector of the station.
/// Must be placed on a grid that has <see cref="DropPodComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DropPodConsoleComponent : Component
{
    /// <summary>
    /// Beacon names (case-insensitive substring match) that cannot be targeted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> BeaconBlacklist = new()
    {
        "Bridge",
        "Vault",
        "Armory",
        "Security",
        "Brig",
        "Brigmedic",
        "Brigmed",
        "Warden",
        "Genpop",
    };

    [DataField, AutoNetworkedField]
    public float AnnouncementLeadTime = 15f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(120);

    [DataField]
    public TimeSpan LastLaunchTime = TimeSpan.Zero;
}

[Serializable, NetSerializable]
public enum DropPodConsoleUiKey : byte { Key }

/// <summary>
/// The four cardinal sectors the operatives can aim the drop pod at.
/// The server picks a random valid beacon within the chosen sector.
/// </summary>
[Serializable, NetSerializable]
public enum DropPodDirection : byte
{
    North,
    East,
    South,
    West,
}

/// <summary>
/// State sent to the client: which sectors have at least one valid (non-blacklisted) landing beacon.
/// </summary>
[Serializable, NetSerializable]
public sealed class DropPodConsoleBuiState : BoundUserInterfaceState
{
    /// <summary>Sectors that have at least one valid beacon.</summary>
    public HashSet<DropPodDirection> AvailableDirections { get; init; } = new();
    public bool CanLaunch { get; init; }
    public bool AlreadyLaunched { get; init; }
    /// <summary>Station grid to display on the nav map.</summary>
    public NetEntity? StationGrid { get; init; }
    /// <summary>World-space centroid of all valid beacons, used for click direction classification.</summary>
    public Vector2 StationWorldCenter { get; init; }
}

/// <summary>
/// Sent by the client to request launching toward a general sector.
/// The server resolves the exact beacon and offset; the operatives never know the precise target.
/// </summary>
[Serializable, NetSerializable]
public sealed class DropPodConsoleDeployMessage : BoundUserInterfaceMessage
{
    public DropPodDirection Direction { get; init; }
}
