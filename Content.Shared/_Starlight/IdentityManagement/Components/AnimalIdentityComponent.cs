using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.IdentityManagement.Components;

/// <summary>
/// Marks an entity as using animal-flavored identity wording ("good boy" etc.)
/// instead of the human age/gender phrasing when its identity is concealed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AnimalIdentityComponent : Component
{
    /// <summary>
    /// Loc key for the bare species noun used when gender is unknown ("dog", "cat", etc.),
    /// substituted into identity-gender-animal-generic.
    /// </summary>
    [DataField]
    public LocId NounId = "identity-animal-noun-dog";
}
