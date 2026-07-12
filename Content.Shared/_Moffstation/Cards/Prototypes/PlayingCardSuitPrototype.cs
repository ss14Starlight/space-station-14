using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._Moffstation.Cards.Prototypes;

[Prototype]
public sealed partial class PlayingCardSuitPrototype : IPrototype, IInheritingPrototype
{
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<PlayingCardSuitPrototype>))]
    public string[]? Parents { get; private set; }

    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    /// Obverse sprite layers which all cards in this deck will have by default. They will be under layers specified on
    /// the card itself.
    [DataField]
    public PrototypeLayerData[] CommonObverseLayers = [];

    /// Whether or not to include <see cref="PlayingCardDeckPrototype.CommonObverseLayers"/> on cards in this suit.
    [DataField]
    public bool UseDeckLayers = true;

    /// The state of the generated obverse sprite layers for cards in this suit. Supports string replacement of <c>{card}</c>.
    [DataField]
    public string? DefaultObverseLayerState;

    /// The cards in this suit.
    [DataField(required: true)]
    public List<PlayingCardDeckPrototypeElementCard> Cards = [];
}
