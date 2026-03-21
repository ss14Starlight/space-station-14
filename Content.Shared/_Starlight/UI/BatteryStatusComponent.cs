using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.UI;

[RegisterComponent]
public sealed partial class BatteryAlertComponent : Component
{
    [DataField]
    public ProtoId<AlertPrototype> BatteryAlert = "BorgBattery";

    [DataField]
    public ProtoId<AlertPrototype> NoBatteryAlert = "BorgBatteryNone";
}