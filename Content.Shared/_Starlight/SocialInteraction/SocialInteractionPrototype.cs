using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.SocialInteraction;

[Prototype]
public sealed partial class SocialInteractionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId VerbName { get; private set; } = default!;

    //ripped mostly from InteractionPopup component

    /// <summary>
    /// Time delay between interactions to avoid spam.
    /// </summary>
    [DataField("interactDelay")]
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan InteractDelay = TimeSpan.FromSeconds(1.0);

    /// <summary>
    /// String will be used to fetch the localized message to be played if the interaction succeeds.
    /// Nullable in case none is specified on the yaml prototype.
    /// </summary>
    [DataField("interactString")]
    public LocId? InteractString;

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
    public LocId? MessagePerceivedByOthers;

    /// <summary>
    /// The emote that will be posted in chat.
    /// </summary>
    [DataField("emoteMessage")]
    public LocId? EmoteMessage;

    /// <summary>
    /// Alternative emote if we end up targeting ourselves instead.
    /// </summary>
    [DataField("emoteMessageSelf")]
    public LocId? EmoteMessageSelf;

    /// <summary>
    /// Will the sound effect be perceived by entities not involved in the interaction?
    /// </summary>
    [DataField("soundPerceivedByOthers")]
    public bool SoundPerceivedByOthers = true;

    /// <summary>
    /// Can you perform this interaction on yourself?
    /// </summary>
    [DataField("allowSelfTarget")]
    public bool AllowSelfTarget = false;

    /// <summary>
    /// Does this social interaction require being within interaction range of the target?
    /// Stuff like 'waving at someone' wouldn't, while patting them would.
    /// </summary>
    [DataField("isPhysical")]
    public bool IsPhysical = true;
}
