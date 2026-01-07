using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Holograms;

/// <summary>
///     Marks the entity as a being made of light.
///     Details determined by sister components.
/// </summary>
[RegisterComponent]
public sealed partial class HologramComponent : Component
{
    /// <summary>
    ///     The sound to play when the Hologram is turned on.
    /// </summary>
    [DataField("onSound"), ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier OnSound = new SoundPathSpecifier("/Audio/_Starlight/Effects/Holograms/holo_on.ogg");

    /// <summary>
    ///     The sound to play when the Hologram is turned off.
    /// </summary>
    [DataField("offSound"), ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier OffSound = new SoundPathSpecifier("/Audio/_Starlight/Effects/Holograms/holo_off.ogg");

    /// <summary>
    ///     The string to use for the popup when the Hologram appears, shown to others.
    /// </summary>
    [DataField("popupAppearOther"), ViewVariables(VVAccess.ReadWrite)]
    public string PopupAppearOther = "system-hologram-phasing-appear-others";

    /// <summary>
    ///     The string to use for the popup when the Hologram appears, shown to themselves.
    /// </summary>
    [DataField("popupAppearSelf"), ViewVariables(VVAccess.ReadWrite)]
    public string PopupAppearSelf = "system-hologram-phasing-appear-self";

    /// <summary>
    ///     The string to use for the popup when the Hologram disappears, shown to others.
    /// </summary>
    [DataField("popupDisappearOther"), ViewVariables(VVAccess.ReadWrite)]
    public string PopupDisappearOther = "system-hologram-phasing-disappear-others";

    /// <summary>
    ///     The string to use for the popup when the Hologram is killed, shown to themselves.
    /// </summary>
    [DataField("popupDeathSelf"), ViewVariables(VVAccess.ReadWrite)]
    public string PopupDeathSelf = "system-hologram-phasing-death-self";

    /// <summary>
    ///     The string to use for the popup when the Hologram fails to interact with something, due to their non-solid nature.
    /// </summary>
    [DataField("popupHoloInteractionFail"), ViewVariables(VVAccess.ReadWrite)]
    public string PopupHoloInteractionFail = "system-hologram-interaction-with-others-fail";

    /// <summary>
    ///     The string to use for the popup when the someone fails to interact with the Hologram, due to their non-holographic nature.
    /// </summary>
    [DataField("popupInteractionWithHoloFail"), ViewVariables(VVAccess.ReadWrite)]
    public string PopupInteractionWithHoloFail = "system-hologram-interaction-with-holo-fail";

    /// <summary>
    ///     A list of tags for the Hologram to collide with, assuming they're not hardlight.
    /// </summary>
    /// <remarks>
    ///     This should generally include the 'Wall' tag.
    /// </remarks>
    [DataField("collideWhitelist"), ViewVariables(VVAccess.ReadWrite)]
    public EntityWhitelist CollideWhitelist = new();
}
