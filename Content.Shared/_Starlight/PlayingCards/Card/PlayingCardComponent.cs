using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.PlayingCards.Card;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlayingCardComponent : Component
{
    [DataField] public List<SpriteSpecifier>? BackSpriteLayers = [];
    [DataField] public List<SpriteSpecifier>? FrontSpriteLayers = [];

    /// <summary>
    /// if card is flipped or not
    /// </summary>
    [DataField, AutoNetworkedField] public bool Flipped;
    
    /// <summary>
    /// name of the card
    /// </summary>
    [DataField, AutoNetworkedField] public string Name;
}