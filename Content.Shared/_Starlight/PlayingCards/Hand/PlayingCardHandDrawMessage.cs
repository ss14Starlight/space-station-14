using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.PlayingCards.Hand;

[Serializable, NetSerializable]
public enum CardUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class PlayingCardHandDrawMessage(NetEntity card) : BoundUserInterfaceMessage
{
    public NetEntity Card = card;
}