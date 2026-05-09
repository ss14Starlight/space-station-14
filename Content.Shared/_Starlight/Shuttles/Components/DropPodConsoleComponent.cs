using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Shuttles.Components;

/// <summary>
/// A console that allows launching a drop pod at a selected FTL beacon on the station.
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

    /// <summary>
    /// How many seconds before impact the warning announcement is sent.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AnnouncementLeadTime = 15f;

    /// <summary>
    /// Minimum time in seconds between two consecutive launches.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(120);

    /// <summary>
    /// When the last launch was initiated.
    /// </summary>
    [DataField]
    public TimeSpan LastLaunchTime = TimeSpan.Zero;
}

[Serializable, NetSerializable]
public enum DropPodConsoleUiKey : byte
{
    Key,
}

/// <summary>
/// State sent from the server to the client listing available (non-blacklisted) FTL beacons.
/// </summary>
[Serializable, NetSerializable]
public sealed class DropPodConsoleBuiState : BoundUserInterfaceState
{
    /// <summary>
    /// Available beacons the drop pod can be aimed at.
    /// </summary>
    public List<DropPodBeaconEntry> Beacons { get; init; } = new();

    /// <summary>
    /// True if the console is on a valid drop pod grid and can launch.
    /// </summary>
    public bool CanLaunch { get; init; }

    /// <summary>
    /// True if the drop pod has already been launched.
    /// </summary>
    public bool AlreadyLaunched { get; init; }
}

/// <summary>
/// Represents a single selectable beacon target.
/// </summary>
[Serializable, NetSerializable]
public sealed class DropPodBeaconEntry
{
    public NetEntity Beacon { get; init; }
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Sent by the client to request launching the drop pod at the selected beacon.
/// </summary>
[Serializable, NetSerializable]
public sealed class DropPodConsoleDeployMessage : BoundUserInterfaceMessage
{
    public NetEntity SelectedBeacon { get; init; }
}
