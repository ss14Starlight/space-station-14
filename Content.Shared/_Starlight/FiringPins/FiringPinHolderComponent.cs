using Content.Shared.Tools;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.FiringPins;

[RegisterComponent, NetworkedComponent]
public sealed partial class FiringPinHolderComponent : Component
{
    /// <summary>
    /// How the pin is removed from the gun
    /// </summary>
    [DataField]
    public ProtoId<ToolQualityPrototype> PinExtractionMethod = "Screwing";

    /// <summary>
    /// What pins actually fit this gun?
    /// </summary>
    [DataField]
    public IReadOnlyList<string> SupportedPins = [ "Rifle" ];

    [DataField]
    public SoundSpecifier PinExtractionSound = new SoundPathSpecifier("/Audio/Items/pistol_magout.ogg");

    [DataField]
    public SoundSpecifier PinInsertionSound = new SoundPathSpecifier("/Audio/Items/pistol_magin.ogg");

    [ViewVariables]
    public Container PinContainer = default!;
    public const string PinContainerName = "firing_pin";
}