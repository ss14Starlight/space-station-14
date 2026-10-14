using System.Linq;
using Content.Shared._Moffstation.Cards.Components;
using Content.Shared._Moffstation.Extensions;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Shared._Moffstation.Cards.Systems;

// This part implements transferring cards from one place to another.
public abstract partial class SharedPlayingCardsSystem
{
    /// Moves the cards specified by <paramref name="range"/> from <paramref name="source"/> to
    /// <paramref name="target"/>. The range is relative to <paramref name="source"/>.
    /// <br/>
    /// If <paramref name="user"/> is not null, audio and a pickup animation will be played.
    public void Transfer<TSource, TTarget>(
        Entity<TSource> source,
        Entity<TTarget> target,
        Range range,
        EntityUid? user
    ) where TSource : PlayingCardStackComponent where TTarget : PlayingCardStackComponent
    {
        DebugTools.AssertNotEqual(source.Owner, target.Owner, "Source and target cannot be the same");

        TransferImpl(
            CardSourceFrom(source),
            Transform(source).Coordinates,
            CardSinkFrom(target),
            Transform(target).Coordinates,
            range,
            user
        );
    }

    /// Removes the cards specified by <paramref name="range"/> from <paramref name="stack"/>, returning them in a list.
    /// <br/>
    /// If <paramref name="user"/> is not null, audio and a pickup animation will be played.
    public List<Entity<PlayingCardComponent>> Take<TStack>(
        Entity<TStack> stack,
        Range range,
        EntityCoordinates destinationCoords,
        EntityUid? user
    ) where TStack : PlayingCardStackComponent
    {
        var ret = new List<Entity<PlayingCardComponent>>();
        TransferImpl(
            CardSourceFrom(stack),
            Transform(stack).Coordinates,
            (cards, _) =>
            {
                ret.AddRange(cards.Select(cardLike => EnsureSpawnedOrNull(cardLike, destinationCoords))
                    .OfType<Entity<PlayingCardComponent>>());
                return ret.FirstOrNull();
            },
            destinationCoords,
            range,
            user
        );
        return ret;
    }

    /// Adds all <paramref name="cards"/> given to <paramref name="stack"/>.
    /// <br/>
    /// If <paramref name="user"/> is not null, audio and a pickup animation will be played.
    public void Add<TStack>(
        Entity<TStack> stack,
        IEnumerable<Entity<PlayingCardComponent>> cards,
        EntityCoordinates cardsCoords,
        EntityUid? user
    // Starlight edit Start: Expression body
    ) where TStack : PlayingCardStackComponent =>
        TransferImpl(
            (range, _) => cards.Take(range).Select(it => new CardLike.Entity(it)),
            cardsCoords,
            CardSinkFrom(stack),
            Transform(stack).Coordinates,
            ..,
            user
        );
    // Starlight edit End: Expression body

    /// Special case of <see cref="Add{TStack}(Entity{TStack}, IEnumerable{Entity{PlayingCardComponent}}, EntityCoordinates, EntityUid?)">Add</see>
    /// which takes exactly one card, allowing for skipping providing the cards' coordinates.
    public void Add<TStack>(
        Entity<TStack> stack,
        Entity<PlayingCardComponent> card,
        EntityUid? user
    ) where TStack : PlayingCardStackComponent => Add(stack, [card], Transform(card).Coordinates, user);

    /// A source of cards for transferring. Cards in the specified <paramref name="range"/> are taken from their current
    /// location and yielded in the returned enumerable. The <paramref name="user"/> is also provided so that things
    /// like predicted audio can be played.
    private delegate IEnumerable<CardLike> CardSource(Range range, EntityUid? user);

    /// A sink for cards for transferring. The given <paramref name="cards"/> are added to this sink, and an entity is
    /// returned to be used for animation the transfer. The <paramref name="user"/> is also provided so that things like
    /// predicted audio can be played.
    private delegate EntityUid? CardSink(IEnumerable<CardLike> cards, EntityUid? user);

    /// This type represents a Card either as an entity with <see cref="PlayingCardComponent"/> or an unspawned card in
    /// a <see cref="PlayingCardDeckComponent"/> during transfers.
    private abstract record CardLike : ISealedInheritance
    {
        // Private constructor to seal inheritance.
        private CardLike() { }

        public sealed record Entity(Entity<PlayingCardComponent> Ent) : CardLike;

        public sealed record Unspawned(PlayingCardInDeck Data) : CardLike;
    }

    /// <summary>
    /// The implementation of all card movement into and out of stacks.
    /// </summary>
    /// <param name="source">The implementation of taking and returning cards from the source</param>
    /// <param name="sourceCoords">Where the source is, conceptually. Movement animations and audio play from here</param>
    /// <param name="sink">The implementation of adding cards to the sink</param>
    /// <param name="sinkCoords">Where the sink is, conceptually. Movement animations and audio play from here</param>
    /// <param name="range">Specification of which cards are taken. This is relative to <paramref name="source"/>.</param>
    /// <param name="user">Who is causing the moving. This is used to make audio / pickup animations predictive. If this
    /// is not specified, neither audio nor pickup animations will occur.</param>
    private void TransferImpl(
        CardSource source,
        EntityCoordinates sourceCoords,
        CardSink sink,
        EntityCoordinates sinkCoords,
        Range range,
        EntityUid? user
    )
    {
        // TODO Contact interaction?

        var pickupAnimationEnt = sink(source(range, user), user);

        if (user is { } u && pickupAnimationEnt is { } ent)
        {
            _storage.PlayPickupAnimation(ent, sourceCoords, sinkCoords, 0, u);
        }
    }

    private CardSource CardSourceFrom<TStack>(Entity<TStack> entity) where TStack : PlayingCardStackComponent =>
        entity.Comp switch
        {
            PlayingCardDeckComponent deck => AsCardSource((entity, deck)),
            PlayingCardHandComponent hand => AsCardSource((entity, hand)),
            _ => entity.Comp.ThrowUnknownInheritor<TStack, CardSource>(),
        };

    private CardSource AsCardSource(Entity<PlayingCardDeckComponent> entity)
    {
        return Impl;

        // Local method to enable `yield`.
        IEnumerable<CardLike> Impl(Range range, EntityUid? user)
        {
            var (inRange, outOfRange) = Split(entity.Comp.Cards, range);
            try
            {
                foreach (var cardInDeck in inRange)
                {
                    switch (cardInDeck)
                    {
                        case PlayingCardInDeckNetEnt(var netEntity):
                            if (NetEntToCard(netEntity) is not { } card)
                                continue;

                            _container.Remove(card.Owner, entity.Comp.Container);
                            yield return new CardLike.Entity(card);
                            break;
                        default:
                            yield return new CardLike.Unspawned(cardInDeck);
                            break;
                    }
                }
            }
            finally
            {
                entity.Comp.Cards = outOfRange;
                entity.Comp.DirtyVisuals = true;
                Dirty(entity);

                _audio.PlayPredicted(entity.Comp.PickUpSound, Transform(entity).Coordinates, user);

                if (entity.Comp.NumCards == 0)
                {
                    PredictedDel(entity.Owner);
                }
            }
        }
    }

    private CardSource AsCardSource(Entity<PlayingCardHandComponent> entity)
    {
        return Impl;

        // Local method to enable `yield`.
        IEnumerable<CardLike> Impl(Range range, EntityUid? user)
        {
            var (inRange, outOfRange) = Split(entity.Comp.Cards, range);
            try
            {
                foreach (var cardNetEnt in inRange)
                {
                    if (NetEntToCard(cardNetEnt) is not { } card)
                        continue;

                    _container.Remove(card.Owner, entity.Comp.Container);

                    yield return new CardLike.Entity(card);
                }
            }
            finally
            {
                entity.Comp.Cards = outOfRange;
                entity.Comp.DirtyVisuals = true;
                Dirty(entity);

                var entityCoordinates = Transform(entity).Coordinates;
                _audio.PlayPredicted(entity.Comp.PickUpSound, entityCoordinates, user);

                if (entity.Comp.NumCards == 0)
                {
                    PredictedQueueDel(entity);
                }
                else if (entity.Comp.NumCards == 1)
                {
                    // Turn into just a card
                    var lastCard = Take(entity, .., entityCoordinates, null).Single();

                    // If the hand was in a container, leave the last card in its place in the container.
                    var cardParent = Transform(entity).ParentUid;
                    if (_container.TryGetContainingContainer(cardParent, entity, out var container))
                    {
                        _container.Remove(entity.Owner, container, force: true);
                        _container.Insert(lastCard.Owner, container);
                    }

                    PredictedQueueDel(entity);
                }
            }
        }
    }

    private CardSink CardSinkFrom<TStack>(Entity<TStack> entity) where TStack : PlayingCardStackComponent
    {
        if (IsClientSide(entity))
            return PredictedCardSinkFrom(entity);

        return entity.Comp switch
        {
            PlayingCardDeckComponent deck => AsCardSink((entity, deck)),
            PlayingCardHandComponent hand => AsCardSink((entity, hand)),
            _ => entity.Comp.ThrowUnknownInheritor<TStack, CardSink>(),
        };
    }

    /// This card sink is used when the card sink entity is predicted and therefore cannot have cards inserted into
    /// itself. It detaches entities from the transform hierarchy until their state is updated by some other operation.
    private CardSink PredictedCardSinkFrom<TStack>(Entity<TStack> entity) where TStack : PlayingCardStackComponent
    {
        var deckCoords = Transform(entity).Coordinates;
        return (cards, user) =>
        {
            Entity<PlayingCardComponent>? first = null;
            var count = 0;
            foreach (var cardLike in cards)
            {
                count += 1;
                if (cardLike is CardLike.Entity(var card))
                {
                    first ??= card;
                    _transform.DetachEntity(card);
                }
            }

            if (count > 0)
            {
                _audio.PlayPredicted(entity.Comp.PlaceDownSound, deckCoords, user);
            }

            return first;
        };
    }

    private CardSink AsCardSink(Entity<PlayingCardDeckComponent> entity) => (cards, user) =>
    {
        var deckCoords = Transform(entity).Coordinates;

        Entity<PlayingCardComponent>? first = null;
        var count = 0;
        foreach (var cardLike in cards)
        {
            count += 1;
            first ??= cardLike is CardLike.Entity(var e) ? e : null;
            switch (cardLike)
            {
                case CardLike.Entity(var ent):
                    entity.Comp.Cards.Add(new PlayingCardInDeckNetEnt(GetNetEntity(ent)));
                    if (!_container.Insert(ent.Owner, entity.Comp.Container, force: true))
                    {
                        this.AssertOrLogError(
                            $"Failed to insert card into deck (card={ToPrettyString(ent)}, hand={ToPrettyString(entity)})"
                        );
                    }

                    break;
                case CardLike.Unspawned(var cardInDeck):
                    entity.Comp.Cards.Add(cardInDeck);
                    break;
                default:
                    cardLike.ThrowUnknownInheritor();
                    break;
            }
        }

        if (count > 0)
        {
            entity.Comp.DirtyVisuals = true;
            Dirty(entity);

            _audio.PlayPredicted(entity.Comp.PlaceDownSound, deckCoords, user);
        }

        return first;
    };

    private CardSink AsCardSink(Entity<PlayingCardHandComponent> entity) => (cards, user) =>
    {
        var coords = Transform(entity).Coordinates;

        Entity<PlayingCardComponent>? first = null;
        var count = 0;
        foreach (var cardLike in cards)
        {
            if (EnsureSpawnedOrNull(cardLike, coords) is not { } spawned)
                continue;
            count += 1;
            first ??= spawned;
            entity.Comp.Cards.Add(GetNetEntity(spawned));
            if (!_container.Insert(spawned.Owner, entity.Comp.Container, force: true))
            {
                this.AssertOrLogError(
                    $"Failed to insert card into hand (card={ToPrettyString(spawned)}, hand={ToPrettyString(entity)})"
                );
            }
        }

        if (count > 0)
        {
            entity.Comp.DirtyVisuals = true;
            Dirty(entity);

            _audio.PlayPredicted(entity.Comp.PlaceDownSound, Transform(entity).Coordinates, user);
        }

        return first;
    };

    private Entity<PlayingCardComponent>? EnsureSpawnedOrNull(CardLike cardLike, EntityCoordinates coords) =>
        cardLike switch
        {
            CardLike.Entity(var entity) => entity,
            CardLike.Unspawned(var card) => card switch
            {
                PlayingCardInDeckUnspawnedData data =>
                    SpawnPredictedDynamicCard(data, coords),
                PlayingCardInDeckUnspawnedRef(var entProtoId, var faceDown) =>
                    PredictedSpawnAtPosition(entProtoId, coords) is var spawnedRef
                        ? (spawnedRef, WithFacing(EnsureComp<PlayingCardComponent>(spawnedRef), faceDown))
                        : throw new("Unreachable: spawned ref pattern is irrefutable"),
                PlayingCardInDeckNetEnt => this.AssertOrLogError<Entity<PlayingCardComponent>?>(
                    $"{nameof(CardLike.Unspawned)} contained net entity. This shouldn't happen because it means that the source likely did not properly remove the card from its storage before yielding it. Instead, sources should yield existing entities as {nameof(CardLike.Entity)}.",
                    null
                ),
                _ => card.ThrowUnknownInheritor<PlayingCardInDeck, Entity<PlayingCardComponent>?>(),
            },
            _ => cardLike.ThrowUnknownInheritor<CardLike, Entity<PlayingCardComponent>?>(),
        };

    /// Splits the given <paramref name="source"/> into two lists, one of elements in the range and one of elements
    /// outside the range.
    private static (List<T> inRange, List<T> outOfRange) Split<T>(ICollection<T> source, Range range)
    {
        var start = range.Start.GetOffset(source.Count);
        var end = range.End.GetOffset(source.Count);

        var inRange = source.Index().Where(it => it.Index >= start && it.Index < end).Select(it => it.Item).ToList();
        var outOfRange = source.Index().Where(it => it.Index < start || it.Index >= end).Select(it => it.Item).ToList();

        return (inRange, outOfRange);
    }
}
