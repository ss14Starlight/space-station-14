using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Component = Robust.Shared.GameObjects.Component;

namespace Content.Shared._Starlight.PlayingCards;

/// <summary>
/// Holds prototype IDs of cards in the stack/hand.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlayingCardStackComponent : Component
{
    [DataField] public List<EntProtoId> Content = [];
    [DataField] public SoundSpecifier ShuffleSound = new SoundCollectionSpecifier("cardFan");
    [DataField] public SoundSpecifier PickUpSound = new SoundCollectionSpecifier("cardSlide");
    [DataField] public SoundSpecifier PlaceDownSound = new SoundCollectionSpecifier("cardShove");

    [ViewVariables] public BaseContainer ItemContainer = default!;

    [DataField, AutoNetworkedField] public List<EntityUid> Cards = [];

    [DataField] public LocId JoinCardsLocId = "card-verb-join";
    [DataField] public LocId OrganizeValueLocId = "card-verb-organize-value";
    [DataField] public LocId OrganizeSuitLocId = "card-verb-organize-suit";
    [DataField] public LocId FlipCardsUpLocId = "card-verb-cards-flip-up";
    [DataField] public LocId FlipCardsDownLocId = "card-verb-cards-flip-down";
    [DataField] public LocId FlipDeckLocId = "card-verb-deck-flip";
    [DataField] public string PopupSuccessSuffix = "success";
}