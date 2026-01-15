using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.PlayingCards.Card;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class PlayingCardComponent : Component
{
    [ViewVariables] public List<SpriteSpecifier>? BackSpriteLayers = [];
    [ViewVariables] public List<SpriteSpecifier>? FrontSpriteLayers = [];

    /// <summary>
    /// Used to determine sprite. 0 is ace. 12 is king.
    /// </summary>
    [DataField, AutoNetworkedField] public int Value;
    /// <summary>
    /// Used to determine sprite.
    /// </summary>
    [DataField, AutoNetworkedField] public CardSuit Suit = CardSuit.Club;
    /// <summary>
    /// If true, flags this card as a joker and ignores the previous two values. Again, used to determine sprite.
    /// </summary>
    [DataField, AutoNetworkedField] public bool Joker;
    /// <summary>
    /// Used to determine back sprite.
    /// </summary>
    [DataField, AutoNetworkedField] public string BackFaceName = "black";
    /// <summary>
    /// Prefix to use for generating name of backface.
    /// </summary>
    [DataField, AutoNetworkedField] public string BackFacePrefix = "singlecard_down_";

    /// <summary>
    /// fallback value, primarily used when initializing a new card, but again also a fallback.
    /// </summary>
    [DataField, AutoNetworkedField] public ResPath RSIPath = new("_Starlight/Objects/Fun/cards.rsi");
    
    /// <summary>
    /// if card is flipped or not
    /// </summary>
    [DataField, AutoNetworkedField] public bool Flipped;
}

public enum CardSuit : byte
{
    Heart,
    Club,
    Diamond,
    Spade
}