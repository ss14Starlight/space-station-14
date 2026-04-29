using Content.Shared._Moffstation.Cards.Systems;
using Content.Shared._Moffstation.Extensions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.Cards.Components;

/// A collection of <see cref="PlayingCardComponent">playing cards</see> which are more accessible and interactible than
/// a <see cref="PlayingCardDeckComponent"/>. Unlike decks, cards in hands are always entities.
/// <seealso cref="SharedPlayingCardsSystem"/>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedPlayingCardsSystem))]
public sealed partial class PlayingCardHandComponent : PlayingCardStackComponent
{
    /// The cards in this hand.
    [AutoNetworkedField]
    public List<NetEntity> Cards = [];

    /// The number of cards in this hand.
    public override int NumCards => Cards.Count;

    /// When constructing the sprite for this hand based on its contents, this is the total angle across which cards are
    /// spread.
    [DataField]
    public float Angle = 120f;

    /// When constructing the sprite for this hand based on its contents, this is kinda like the radius of the arc
    /// across which the cards are spread.
    [DataField]
    public float XOffset = 0.5f;

    /// The visual scale of cards when constructing a sprite for this hand based on its contents.
    [DataField]
    public float Scale = 1;

    /// Sprite layers added to this entity based on contained cards' <see cref="PlayingCardComponent.Sprite"/>.
    [ViewVariables]
    public HashSet<string> SpriteLayersAdded = [];

    [ViewVariables, AutoNetworkedField]
    public NetEntity? PickedCardDestination;

    public static readonly LocId CardsAddedText = "playing-cards-hand-card-count-changed-added";
    public static readonly LocId CardsRemovedText = "playing-cards-hand-card-count-changed-removed";
    public static readonly LocId CardsChangedText = "playing-cards-hand-card-count-changed-unknown";

    public new static class Verbs
    {
        public static readonly VerbInfo CardPickup = VerbInfo.Build("playing-card-hand-card-pickup", icon: "pickup");
        public static readonly VerbInfo StackPickup = VerbInfo.Build("playing-card-hand-stack-pickup", icon: "pickup");

        public static readonly VerbInfo StackPickupEntire = VerbInfo.Build("playing-card-hand-stack-pickup-entire", icon: "insert");

        public static readonly VerbInfo ConvertToDeck = VerbInfo.Build("playing-card-hand-convert-to-deck", icon: "rotate_cw");
    }
}

/// The value used to key the UI state for a <see cref="PlayingCardHandComponent"/>.
[Serializable, NetSerializable]
public enum PlayingCardHandUiKey : byte { Key }
