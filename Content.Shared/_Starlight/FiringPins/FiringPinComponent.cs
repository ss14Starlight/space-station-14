using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.FiringPins;

[RegisterComponent]
public sealed partial class FiringPinComponent : Component
{
    /// <summary>
    /// What kind of pin is this? determines what guns it fits in
    /// </summary>
    [DataField]
    public string PinType = "Rifle";
}