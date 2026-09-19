using Content.Shared.Humanoid.Markings;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// A fixed face for a tutorial NPC: the default body for its species, plus the hair it was written
/// with.
/// </summary>
/// <remarks>
/// The alternative is <c>RandomHumanoidAppearance</c>, which is how most NPCs get a face, and it is
/// wrong here. A coach is a character the player is meant to remember, and randomising him rolls a
/// new one every round, including the combinations nobody would have picked. Everything not named
/// below stays at the species default.
/// </remarks>
[RegisterComponent, NetworkedComponent]
public sealed partial class TutorialAppearanceComponent : Component
{
    /// <summary>Hair marking to wear, e.g. <c>HumanHairShortHair</c>. Unset leaves them bald.</summary>
    [DataField]
    public ProtoId<MarkingPrototype>? Hair;

    /// <summary>Facial hair marking, e.g. <c>HumanFacialHairStubble</c>.</summary>
    [DataField]
    public ProtoId<MarkingPrototype>? FacialHair;

    /// <summary>
    /// Colour for both hair markings. One field because a character with two different colours of
    /// hair on one head is the thing this component exists to avoid.
    /// </summary>
    [DataField]
    public Color HairColor = Color.FromHex("#3B2A20");
}
