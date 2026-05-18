using Content.Shared._Moffstation.Cards.Components;
using Content.Shared._Moffstation.Extensions;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;
using Robust.Shared.Utility;

namespace Content.Shared._Moffstation.Cards.Prototypes;

/// The description of a deck of cards. This is used to define a collection of cards all at once with minimal
/// duplication in the YAML.
[Prototype]
public sealed partial class PlayingCardDeckPrototype : IPrototype, IInheritingPrototype
{
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<PlayingCardDeckPrototype>))]
    public string[]? Parents { get; private set; }

    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    /// The RSI which cards in this deck will use by default.
    [DataField("sprite", required: true)]
    public ResPath RsiPath;

    /// Obverse sprite layers which all cards in this deck will have by default. They will be under layers specified on
    /// the card itself.
    [DataField]
    public PrototypeLayerData[] CommonObverseLayers = [];

    /// If a card does not specify its own layers, this string is used as its state. This supports replacing
    /// <c>{suit}</c> and <c>{card}</c> with the suit ID and card ID respectively of the card in question.
    [DataField]
    public string? DefaultObverseLayerState;

    /// Reverse sprite layers which all cards in this deck will have by default.
    [DataField(required: true)]
    public PrototypeLayerData[] CommonReverseLayers = default!;

    /// Localization string to use for the card's name. It is passed the <see cref="CardValueLoc"><c>card</c></see> and
    /// <see cref="SuitLoc">><c>suit</c></see> of the card in question.
    [DataField(required: true)] public LocId CardNameLoc;

    /// Localization string to use for the card's description. It is passed the
    /// <see cref="CardValueLoc"><c>card</c></see> and <see cref="SuitLoc">><c>suit</c></see> of the card in question.
    [DataField(required: true)] public LocId CardDescLoc;

    /// Localization string to use for the card when it's reversed. It is passed the
    /// <see cref="CardValueLoc"><c>card</c></see> and <see cref="SuitLoc">><c>suit</c></see> of the card in question.
    [DataField(required: true)] public LocId CardReverseNameLoc;

    /// Localization string to use for the card's description when it's reversed. It is passed the
    /// <see cref="CardValueLoc"><c>card</c></see> and <see cref="SuitLoc">><c>suit</c></see> of the card in question.
    [DataField(required: true)] public LocId CardReverseDescLoc;

    /// Localization string to use for suit names.
    [DataField(required: true)] public LocId SuitLoc;

    /// Localization string to use for card <b>values</b>.
    [DataField(required: true)] public LocId CardValueLoc;

    /// The contents of this deck, these are either immediately cards or are references to
    /// <see cref="PlayingCardSuitPrototype"/>.
    /// <seealso cref="Element"/>
    [DataField(required: true)]
    public List<Element> Cards = default!;

    /// Logically, a card within in a <see cref="PlayingCardDeckPrototype"/>. This can either just defer to an existing entity
    /// prototype or be minimal information about a card to be completed by information on the deck prototype. This enables
    /// easy definition of cards as part of a deck with lots of shared parts while also enabling an "escape hatch" to say
    /// "I don't want anything done for me, just put this existing card prototype in the deck".
    [ImplicitDataDefinitionForInheritors, Serializable, NetSerializable]
    public abstract partial class Element : ISealedInheritance;
}

/// A <see cref="PlayingCardDeckPrototype.Element"/> which refers to an existing card prototype. Whatever that prototype is,
/// it'll be stuck in the deck. I hope it has the card component :^)
[Serializable, NetSerializable]
public sealed partial class PlayingCardDeckPrototypeElementPrototypeReference : PlayingCardDeckPrototype.Element
{
    public const string PrototypeKey = "prototype";

    [DataField(PrototypeKey, required: true)]
    public EntProtoId<PlayingCardComponent> Prototype;

    /// If the card should spawn in the deck facing down.
    [DataField]
    public bool FaceDown = true; // Starlight: All cards should start face down.

    /// How many copies of this card should be included. Note that this is ONLY used when initializing a deck entity's
    /// contents.
    [DataField]
    public int Count = 1;
}

/// A <see cref="PlayingCardDeckPrototype.Element"/> which will construct a card entity with defaults specified on the deck
/// and finished by the information in this data definition.
[Serializable, NetSerializable]
public sealed partial class PlayingCardDeckPrototypeElementCard : PlayingCardDeckPrototype.Element
{
    public const string IdKey = "id";

    [DataField(IdKey, required: true)]
    public string Id = default!;

    /// A card-specific override for <see cref="PlayingCardDeckPrototype.CardNameLoc"/>.
    [DataField]
    public LocId? NameLoc;

    /// Card-specific override for <see cref="PlayingCardDeckPrototype.DefaultObverseLayerState"/>, this allows for more
    /// complicated sprites on specific cards.
    /// States of layers in this array support string replacement of <c>{suit}</c> with the suit ID, if relevant.
    [DataField]
    public PrototypeLayerData[]? ObverseLayers;

    /// Whether or not to include <see cref="PlayingCardDeckPrototype.CommonObverseLayers"/>. If null, defaults to
    /// <see cref="PlayingCardSuitPrototype.UseDeckLayers"/>, or true if no suit exists.
    [DataField]
    public bool? UseDeckLayers;

    /// Whether or not to include <see cref="PlayingCardSuitPrototype.CommonObverseLayers"/>.
    [DataField]
    public bool UseSuitLayers = true;

    /// If the card should spawn in the deck facing down.
    [DataField]
    public bool FaceDown = true; // Starlight-edit: Cards start in the deck face-down.

    /// How many copies of this card should be included. Note that this is ONLY used when initializing a deck entity's
    /// contents.
    [DataField]
    public int Count = 1;
}

/// A <see cref="PlayingCardDeckPrototype.Element"/> which includes all cards in the referenced suit in the deck.
[Serializable, NetSerializable]
public sealed partial class PlayingCardDeckPrototypeElementSuit : PlayingCardDeckPrototype.Element
{
    public const string SuitKey = "suit";

    [DataField(SuitKey, required: true)]
    public ProtoId<PlayingCardSuitPrototype> Suit;
}

/// This custom serializer enables YAMLers to not need to specify the type of cards in the deck prototype, so long
/// as they have the right fields to implicitly discriminate between the variants.
/// I LOVE IMPLICIT POLYMORPHIC DESERIALIZATION!!
[TypeSerializer]
public sealed class PlayingCardDeckContentContentSerializer : ITypeSerializer<PlayingCardDeckPrototype.Element, MappingDataNode>
{
    public ValidationNode Validate(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null
    )
    {
        if (node.Has(PlayingCardDeckPrototypeElementPrototypeReference.PrototypeKey))
            return serializationManager.ValidateNode<PlayingCardDeckPrototypeElementPrototypeReference>(node, context);

        if (node.Has(PlayingCardDeckPrototypeElementCard.IdKey))
            return serializationManager.ValidateNode<PlayingCardDeckPrototypeElementCard>(node, context);

        if (node.Has(PlayingCardDeckPrototypeElementSuit.SuitKey))
            return serializationManager.ValidateNode<PlayingCardDeckPrototypeElementSuit>(node, context);

        return new ErrorNode(node, "Discrimination of polymorphic YAML failed. Specify the type manually.");
    }

    public PlayingCardDeckPrototype.Element Read(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<PlayingCardDeckPrototype.Element>? instanceProvider = null
    )
    {
        if (node.Has(PlayingCardDeckPrototypeElementPrototypeReference.PrototypeKey))
            return serializationManager.Read<PlayingCardDeckPrototypeElementPrototypeReference>(
                node,
                context,
                notNullableOverride: true
            );

        if (node.Has(PlayingCardDeckPrototypeElementCard.IdKey))
            return serializationManager.Read<PlayingCardDeckPrototypeElementCard>(
                node,
                context,
                notNullableOverride: true
            );

        if (node.Has(PlayingCardDeckPrototypeElementSuit.SuitKey))
            return serializationManager.Read<PlayingCardDeckPrototypeElementSuit>(
                node,
                context,
                notNullableOverride: true
            );

        return (PlayingCardDeckPrototype.Element)serializationManager.Read(
            typeof(PlayingCardDeckPrototype.Element),
            node,
            context,
            notNullableOverride: true
        )!;
    }

    public DataNode Write(
        ISerializationManager serializationManager,
        PlayingCardDeckPrototype.Element value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null
    ) => value switch
    {
        PlayingCardDeckPrototypeElementCard card => Write(
            serializationManager,
            card,
            dependencies,
            alwaysWrite,
            context
        ),
        PlayingCardDeckPrototypeElementPrototypeReference protoRef => Write(
            serializationManager,
            protoRef,
            dependencies,
            alwaysWrite,
            context
        ),
        PlayingCardDeckPrototypeElementSuit suit => Write(
            serializationManager,
            suit,
            dependencies,
            alwaysWrite,
            context
        ),
        _ => value.ThrowUnknownInheritor<PlayingCardDeckPrototype.Element, MappingDataNode>(),
    };
}
