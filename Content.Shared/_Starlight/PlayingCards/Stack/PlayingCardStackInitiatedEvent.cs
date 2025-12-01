using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.PlayingCards;

[Serializable, NetSerializable]
public sealed class PlayingCardStackInitiatedEvent(NetEntity cardStack) : EntityEventArgs
{
    public NetEntity CardStack = cardStack;
}