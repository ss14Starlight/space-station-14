using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.PhysicalSocialInteraction;

[Prototype]
public sealed partial class PhysicalSocialInteractionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string VerbName { get; private set; } = default!;

    //ripped mostly from InteractionPopup component

    /// <summary>
    /// String will be used to fetch the localized message to be played if the interaction succeeds.
    /// Nullable in case none is specified on the yaml prototype.
    /// </summary>
    [DataField("interactString")]
    public string? InteractString;

    /// <summary>
    /// Sound effect to be played when the interaction succeeds.
    /// Nullable in case no path is specified on the yaml prototype.
    /// </summary>
    [DataField("interactSound")]
    public SoundSpecifier? InteractSound;

    /// <summary>
    /// If set, shows a message to all surrounding players but NOT the current player.
    /// </summary>
    [DataField("messagePerceivedByOthers")]
    public string? MessagePerceivedByOthers;

    /// <summary>
    /// Will the sound effect be perceived by entities not involved in the interaction?
    /// </summary>
    [DataField("soundPerceivedByOthers")]
    public bool SoundPerceivedByOthers = true;
}
