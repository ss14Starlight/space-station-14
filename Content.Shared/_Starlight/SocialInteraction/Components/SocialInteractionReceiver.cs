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
    /// - If the target has BoopNose  -> "you boop (target) on (their) nose."
    /// - If the target has BoopSnoot -> "you boop (target) on (their) snoot."
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public List<ProtoId<SocialInteractionPrototype>> InteractionPrototypes = [];
}
