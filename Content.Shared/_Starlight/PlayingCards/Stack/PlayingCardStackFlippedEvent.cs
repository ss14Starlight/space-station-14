using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.PlayingCards;

[Serializable, NetSerializable]
public sealed class PlayingCardStackFlippedEvent(NetEntity cardStack) : EntityEventArgs
{
    public NetEntity CardStack = cardStack;
}

[Serializable, NetSerializable]
public sealed class PlayingCardStackDeckFlippedEvent(NetEntity cardStack) : EntityEventArgs
{
    public NetEntity CardStack = cardStack;
}