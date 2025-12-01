using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.PlayingCards.Card;

[Serializable, NetSerializable]
public sealed class PlayingCardFlipUpdatedEvent(NetEntity card) : EntityEventArgs
{
    public NetEntity Card = card;
}