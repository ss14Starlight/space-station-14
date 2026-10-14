using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.SocialInteraction.Components;

[RegisterComponent]
public sealed partial class SocialInteractionGiverComponent : Component
{
    /// <summary>
    /// List of social interaction prototypes that an entity with the Giver component add to the Verb list.
    ///
    /// A Giver is able to do any SocialInteraction on any target, regardless of what that target entity is.
    /// e.g. You can wave at anything - even if it doesn't have a SocialInteractionReceiverComponent component.
    ///
    /// Use this for interactions that you can reasonable do to anything (looking at something, waving at something, etc.)
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public List<ProtoId<SocialInteractionPrototype>> InteractionPrototypes = [];

    /// <summary>
    /// Stores the last time this Giver did a social interaction.
    /// Needed to prevent Givers from spamming social interactions.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan? LastInteractTime;
}
