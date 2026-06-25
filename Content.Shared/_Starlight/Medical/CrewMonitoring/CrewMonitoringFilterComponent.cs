using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Medical.CrewMonitoring;

[RegisterComponent, NetworkedComponent]
public sealed partial class CrewMonitoringFilterComponent : Component
{
    /// <summary>
    ///     List of departments which this console can see. If empty, unrestricted.
    /// </summary>
    [DataField("shownDepartments"), ViewVariables(VVAccess.ReadWrite)]
    public List<string> ShownDepartments = new();

    /// <summary>
    ///     Always show crew with command tracking implants
    /// </summary>
    [DataField("alwaysShowCommandTrackingImplants"), ViewVariables(VVAccess.ReadWrite)]
    public bool AlwaysShowCommandTrackingImplants = false;

    /// <summary>
    ///     Only show crew who are wounded or dead.
    /// </summary>
    [DataField("onlyShowWoundedOrDead"), ViewVariables(VVAccess.ReadWrite)]
    public bool OnlyShowWoundedOrDead = false;

    /// <summary>
    ///     List of factions the console can see.
    /// </summary>
    [DataField("shownFactions"), ViewVariables(VVAccess.ReadWrite)]
    public List<string> ShownFactions = new() { "crew" };
}
