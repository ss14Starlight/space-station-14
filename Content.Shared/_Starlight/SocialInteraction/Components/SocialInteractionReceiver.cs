using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.SocialInteraction.Components;

[RegisterComponent]
public sealed partial class SocialInteractionReceiverComponent : Component
{
    //list of all valid physical social interaction prototypes
    /// <summary>
    /// List of all valid physical social interaction prototypes that can be used with this receiver.
    /// Anything defined in this list will be ADDED to the parents list, if it exists
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public List<ProtoId<SocialInteractionPrototype>> InteractionPrototypes = new();
}
