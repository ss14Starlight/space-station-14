using Robust.Shared.Audio;
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

    /// <summary>
    /// What this pin turns into when the gun overheats. Falls back to the gun's melted pin.
    /// </summary>
    [DataField]
    public EntProtoId? MeltedPrototype;

    /// <summary>
    /// Popup shown when the pin is destroyed by heat.
    /// </summary>
    [DataField]
    public LocId MeltedPopup = "gun-heat-pin-melted";

    /// <summary>
    /// Sound of the pin being destroyed by heat. Falls back to the gun's melt sound.
    /// </summary>
    [DataField]
    public SoundSpecifier? MeltedSound;
}
