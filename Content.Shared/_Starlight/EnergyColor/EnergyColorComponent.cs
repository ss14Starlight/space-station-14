using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.EnergyColor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), Access(typeof(EnergyColorSystem))]
public sealed partial class EnergyColorComponent : Component
{
    /// <summary>
    /// The color used when activated.
    /// </summary>
    [DataField("color"), AutoNetworkedField] public Color? ActiveColor;

    /// <summary>
    /// A default list of colors to select from on MapInit if <see cref="ActiveColor"/> is null.
    /// </summary>
    // ReSharper disable once UseCollectionExpression - Client sandboxing skill issue.
    [DataField] public List<Color> ColorOptions = new()
    {
        Color.Tomato,
        Color.DodgerBlue,
        Color.Aqua,
        Color.MediumSpringGreen,
        Color.MediumOrchid
    };

    /// <summary>
    /// How fast the RGB will make a full cycle per second (e.g. 1 = one full RGB cycle every second).
    /// </summary>
    [DataField, AutoNetworkedField] public float CycleRate = 1;

    /// <summary>
    /// Whether the item can be hacked to be RGB or not at all.
    /// </summary>
    [DataField, AutoNetworkedField] public bool CanHack = true;

    /// <summary>
    /// If the item can be hacked at all, whether it currently can or if another step is required.
    /// </summary>
    [DataField, AutoNetworkedField] public bool HackingLocked;

    /// <summary>
    /// Tool quality required to unlock hacking as an extra step.
    /// </summary>
    [DataField, AutoNetworkedField] public ProtoId<ToolQualityPrototype>? HackingUnlockQuality;

    /// <summary>
    /// Popup to indicate that hacking the tool is locked.
    /// </summary>
    [DataField, AutoNetworkedField] public string? HackingLockedPopup = "energy-color-hacking-locked";

    /// <summary>
    /// Popup to indicate that hacking the tool has been locked/unlocked.
    /// </summary>
    [DataField, AutoNetworkedField] public string? HackingLockStatePopup = "energy-color-hacking-locked-status";

    /// <summary>
    /// Whether the item has successfully been hacked or not.
    /// </summary>
    [DataField, AutoNetworkedField] public bool Hacked;

    /// <summary>
    /// Tool quality required to successfully hack the item.
    /// </summary>
    [DataField, AutoNetworkedField] public ProtoId<ToolQualityPrototype> HackingQuality = "Pulsing";
}
