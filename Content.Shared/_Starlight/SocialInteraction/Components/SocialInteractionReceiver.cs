using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.SocialInteraction.Components;

[RegisterComponent]
public sealed partial class SocialInteractionReceiverComponent : Component
{
    /// <summary>
    /// List of social interaction prototypes that an entity with the Receiver component add to the Verb list.
    ///
    /// The Receiver component is only on the 'targeted' entity of a SocialInteraction, and is to be used
    /// to define SPECIFIC SocialInteractions that can be 'performed' on this entity.
    ///
    /// e.g.
    /// - If the target has Boop -> "you boop (target) on (their) nose."
    /// - If the target *doesn't* have Boop, you can't Boop 'em.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public List<ProtoId<SocialInteractionPrototype>> InteractionPrototypes = [];

    /// <summary>
    /// Optional per-entity overrides for social interaction prototypes.
    /// Useful if you want a social interaction on a target, but they need an 'alt' string for those interactions.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public List<SocialInteractionOverride> InteractionOverrides = [];
}

[DataDefinition]
public sealed partial class SocialInteractionOverride
{
    /// <summary>
    /// The social interaction prototype to override.
    /// </summary>
    [DataField]
    public ProtoId<SocialInteractionPrototype> ID;

    [DataField("interactString")]
    public LocId? InteractString;

    [DataField("interactSound")]
    public SoundSpecifier? InteractSound;

    [DataField("messagePerceivedByOthers")]
    public LocId? MessagePerceivedByOthers;

    [DataField("emoteMessage")]
    public LocId?EmoteMessage;

    [DataField("emoteMessageSelf")]
    public LocId? EmoteMessageSelf;
}
