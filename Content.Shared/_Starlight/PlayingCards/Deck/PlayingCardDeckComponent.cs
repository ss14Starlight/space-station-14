using Robust.Shared.Audio;

namespace Content.Shared._Starlight.PlayingCards.Deck;

[RegisterComponent]
public sealed partial class PlayingCardDeckComponent : Component
{
    [DataField] public SoundSpecifier ShuffleSound = new SoundCollectionSpecifier("cardFan");
    [DataField] public SoundSpecifier PickUpSound =  new SoundCollectionSpecifier("cardSlide");
    [DataField] public SoundSpecifier PlaceDownSound =   new SoundCollectionSpecifier("cardShove");

    [DataField] public float YOffset = 0.02f;
    [DataField] public float Scale = 1;
    [DataField] public int CardLimit = 5;

    [DataField] public LocId OrganizeValueLocId = "card-verb-organize-value";
    [DataField] public LocId OrganizeSuitLocId = "card-verb-organize-suit";
    [DataField] public LocId FlipCardsUpLocId = "card-verb-cards-flip-up";
    [DataField] public LocId FlipCardsDownLocId = "card-verb-cards-flip-down";
    [DataField] public LocId FlipDeckLocId = "card-verb-deck-flip";
    [DataField] public string PopupSuccessSuffix = "success";
}