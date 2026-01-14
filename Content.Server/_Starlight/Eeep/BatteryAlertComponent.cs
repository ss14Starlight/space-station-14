using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Eeep;

/// <summary>
/// Makes an entity with PredictedBatteryComponent show a battery alert.
/// Requires PredictedBatteryComponent to function.
/// </summary>
[RegisterComponent]
public sealed partial class BatteryAlertComponent : Component
{
    /// <summary>
    /// The alert to show for battery level.
    /// Defaults to AiBattery.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> Alert = "AiBattery";
}
