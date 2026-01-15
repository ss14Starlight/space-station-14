using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.PlayingCards;

[Serializable, NetSerializable]
public sealed class PlayingCardStackReorderedEvent(NetEntity cardStack) : EntityEventArgs
{
    public NetEntity CardStack = cardStack;
}

[Serializable, NetSerializable]
public sealed class PlayingCardStackOrganizedEvent(NetEntity cardStack) : EntityEventArgs
{
    public NetEntity CardStack = cardStack;
}