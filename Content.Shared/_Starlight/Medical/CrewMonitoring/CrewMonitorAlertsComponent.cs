using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Medical.CrewMonitoring;

/// <summary>
///     Allows crew monitors to enable and disable their alerting.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CrewMonitorAlertsComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;
}
