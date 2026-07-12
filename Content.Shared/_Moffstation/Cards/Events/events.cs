using Content.Shared._Moffstation.Cards.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.Cards.Events;

/// This is raised on an entity when a card parented to it is flipped. This is useful for informing card stacks that
/// they need to update their visuals because a card's sprite has changed.
[ByRefEvent]
public record struct ContainedPlayingCardFlippedEvent;

/// This is raised on a card when it is flipped.
[ByRefEvent]
public record struct PlayingCardFlippedEvent;

/// This even is raised on a card stack when its contents are changed.
[ByRefEvent]
public record struct PlayingCardStackContentsChangedEvent(StackQuantityChangeType Type, EntityUid? User);

/// The type of change of a <see cref="PlayingCardStackContentsChangedEvent"/>
[Serializable, NetSerializable]
public enum StackQuantityChangeType : sbyte
{
    Added,
    Removed,
}

/// A message sent from the UI of <see cref="Components.PlayingCardHandComponent"/> indicating the actor wants to remove
/// the card specified by <see cref="Card"/> from a hand of cards, putting that card into the (manipulation) hand with
/// <see name="HandId"/>.
[Serializable, NetSerializable]
public sealed class DrawPlayingCardFromHandMessage(NetEntity card) : BoundUserInterfaceMessage
{
    public NetEntity Card = card;
}

/// This event is raised on <see cref="PlayingCardHandComponent.PickedCardDestination"/> when a <see cref="DrawPlayingCardFromHandMessage"/>
/// is received. Receiving and handling this event means an entity should accept the picked card.
[ByRefEvent]
public sealed class PlayingCardPickedEvent(Entity<PlayingCardHandComponent> sourceHand, Range range, EntityUid user) : HandledEntityEventArgs
{
    public readonly Entity<PlayingCardHandComponent> SourceHand = sourceHand;
    public readonly Range Range = range;
    public readonly EntityUid User = user;
}
