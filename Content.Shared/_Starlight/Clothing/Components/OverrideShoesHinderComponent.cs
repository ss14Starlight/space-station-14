using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Clothing.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class OverrideShoesHinderComponent : Component
{
    /// <summary>
    /// Multiples the final hinder modifier cased by MovementBodyPartHinderedByShoes, for sure with less or greater effects on felionoids and spegs
    /// </summary>
    [DataField("Modifier")]
    public float HinderModifier = 0.0f;
}
