using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.PlayingCards;

[Serializable, NetSerializable]
public enum PlayingCardStackQuantityChangeType : sbyte
{
    Added,
    Removed,
}

[Serializable, NetSerializable]
public sealed class PlayingCardStackQuantityChangeEvent(NetEntity cardStack, NetEntity? card, PlayingCardStackQuantityChangeType type) : EntityEventArgs
{
    public NetEntity CardStack = cardStack;
    public NetEntity? Card = card;
    public PlayingCardStackQuantityChangeType Type = type;
}