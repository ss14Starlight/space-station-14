using Content.Shared._Moffstation.Cards.Prototypes;
using Content.Shared._Moffstation.Cards.Systems;
using Content.Shared._Moffstation.Extensions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Moffstation.Cards.Components;

/// A collection of <see cref="PlayingCardComponent">playing cards</see>. Note that because decks of cards can contain
/// many tens of entities, the implementation aggressively tries to <see cref="PlayingCardInDeck">lazily instantiate the cards
/// contained within</see>.
/// <seealso cref="SharedPlayingCardsSystem"/>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedPlayingCardsSystem))]
public sealed partial class PlayingCardDeckComponent : PlayingCardStackComponent
{
    /// The cards in this deck. Order is important, and is such that the first card in the list is on the bottom of the
    /// deck, and the last card in the list is on the top of the deck. This means that the push/pop behavior of
    /// interacting with the deck should be minimally deleterious to performance.
    [ViewVariables, AutoNetworkedField]
    public List<PlayingCardInDeck> Cards = [];

    /// How many cards are in <see cref="Cards"/>.
    public override int NumCards => Cards.Count;

    /// The card at the top of the deck, that is the one which would be drawn next, or the one whose sprite is most
    /// visible.
    public PlayingCardInDeck? TopCard => NumCards > 0 ? Cards[^1] : null;

    /// The prototype of this deck. This is used to define the contents of a deck in YAML. Note that this is nullable as
    /// arbitrary cards which are not from the same original deck can be joined to create a deck later.
    /// contained in <see cref="Cards"/>.
    [DataField]
    public ProtoId<PlayingCardDeckPrototype>? Prototype;

    /// The visual offset between individual cards when constructing a sprite for this deck based on its contents.
    [DataField]
    public float YOffset = 0.02f;

    /// The visual scale of cards when constructing a sprite for this deck based on its contents.
    [DataField]
    public float Scale = 1;

    /// Sprite layers added to this entity based on contained cards' <see cref="PlayingCardComponent.Sprite"/>.
    [ViewVariables]
    public HashSet<string> SpriteLayersAdded = [];

    public static readonly LocId TopCardExamineLoc = "playing-card-deck-examine";

    public new static class Verbs
    {
        public static readonly VerbInfo CardPickup = VerbInfo.Build("playing-card-deck-card-pickup", icon: "pickup");
        public static readonly VerbInfo StackPickup = VerbInfo.Build("playing-card-deck-stack-pickup", icon: "pickup");

        public static readonly VerbInfo DrawCard = VerbInfo.Build("playing-card-deck-draw", icon: "pickup");
        public static readonly VerbInfo CutDeck = VerbInfo.Build("playing-card-deck-cut", icon: "eject");

        public static readonly VerbInfo FlipEntire =
            VerbInfo.Build("playing-card-deck-flip-entire", icon: "flip", sounds: "storageRustle");
    }
}

/// A type representing a card in a <see cref="PlayingCardDeckComponent"/>. This may be an entity, or it may be
/// information about a card which has not yet been spawned.
[ImplicitDataDefinitionForInheritors, Serializable, NetSerializable]
public abstract partial class PlayingCardInDeck : ISealedInheritance;

// Constructor and deconstructor because these were originally `record`s, but ss14 generation seems broken and I couldn't get it to work.
/// An existing entity. This SHOULD always have a <see cref="PlayingCardComponent"/> on it.
[Serializable, NetSerializable]
public sealed partial class PlayingCardInDeckNetEnt : PlayingCardInDeck
{
    public PlayingCardInDeckNetEnt(NetEntity ent)
    {
        Ent = ent;
    }

    [DataField(required: true)]
    public NetEntity Ent;

    public void Deconstruct(out NetEntity ent)
    {
        ent = Ent;
    }
}

// Constructor and deconstructor because these were originally `record`s, but ss14 generation seems broken and I couldn't get it to work.
/// An unspawned <see cref="PlayingCardDeckPrototypeElementPrototypeReference"/>.
[Serializable, NetSerializable]
public sealed partial class PlayingCardInDeckUnspawnedRef : PlayingCardInDeck
{
    public PlayingCardInDeckUnspawnedRef(
        EntProtoId<PlayingCardComponent> prototype,
        bool faceDown
    )
    {
        Prototype = prototype;
        FaceDown = faceDown;
    }

    [DataField(required: true)]
    public EntProtoId<PlayingCardComponent> Prototype;

    [DataField]
    public bool FaceDown;

    public void Deconstruct(
        out EntProtoId<PlayingCardComponent> prototype,
        out bool faceDown
    )
    {
        prototype = Prototype;
        faceDown = FaceDown;
    }
}

// Constructor and deconstructor because these were originally `record`s, but ss14 generation seems broken and I couldn't get it to work.
/// An unspawned <see cref="PlayingCardDeckPrototypeElementCard"/>.
[Serializable, NetSerializable]
public sealed partial class PlayingCardInDeckUnspawnedData : PlayingCardInDeck
{
    public PlayingCardInDeckUnspawnedData(
        PlayingCardDeckPrototypeElementCard card,
        ProtoId<PlayingCardDeckPrototype> deck,
        ProtoId<PlayingCardSuitPrototype>? suit
    )
    {
        Card = card;
        Deck = deck;
        Suit = suit;
    }

    [DataField(required: true)]
    public PlayingCardDeckPrototypeElementCard Card;

    [DataField(required: true)]
    public ProtoId<PlayingCardDeckPrototype> Deck;

    [DataField]
    public ProtoId<PlayingCardSuitPrototype>? Suit;

    public void Deconstruct(
        out PlayingCardDeckPrototypeElementCard card,
        out ProtoId<PlayingCardDeckPrototype> deck,
        out ProtoId<PlayingCardSuitPrototype>? suit
    )
    {
        card = Card;
        deck = Deck;
        suit = Suit;
    }
}
