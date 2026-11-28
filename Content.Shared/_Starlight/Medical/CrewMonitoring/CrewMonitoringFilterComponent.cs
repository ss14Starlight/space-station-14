using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Medical.CrewMonitoring;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class CrewMonitoringFilterComponent : Component
{
    /// <summary>
    ///     List of departments which this console can see. If empty, unrestricted.
    /// </summary>
    [DataField("shownDepartments"), ViewVariables(VVAccess.ReadWrite)]
    public List<string> ShownDepartments = new();

    /// <summary>
    ///     Always show crew with tracking implants in addition.
    /// </summary>
    [DataField("alwaysShowTrackingImplants"), ViewVariables(VVAccess.ReadWrite)]
    public bool AlwaysShowTrackingImplants = false;

    /// <summary>
    ///     Shows only the crew wearing the specific tracking implant.
    /// </summary>
    [DataField("shownTrackingImplant"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public string? ShownTrackingImplant = default;

    /// <summary>
    ///     The given tracking implants that the monitor can see.
    ///  </summary>
    [DataField("allowedTrackingImplants"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public List<string[]> AllowedTrackingImplants = new() {};

    /// <summary>
    ///     Only show crew who are wounded or dead.
    /// <summary>
    [DataField("onlyShowWoundedOrDead"), ViewVariables(VVAccess.ReadWrite)]
    public bool OnlyShowWoundedOrDead = false;

    /// <summary>
    ///     List of factions the console can see.
    /// </summary>
    [DataField("shownFactions"), ViewVariables(VVAccess.ReadWrite)]
    public List<string> ShownFactions = new() { "crew" };
}
