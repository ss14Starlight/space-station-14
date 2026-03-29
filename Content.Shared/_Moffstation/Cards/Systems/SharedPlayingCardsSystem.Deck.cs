using System.Linq;
using Content.Shared._Moffstation.Cards.Components;
using Content.Shared._Moffstation.Cards.Events;
using Content.Shared._Moffstation.Cards.Prototypes;
using Content.Shared._Moffstation.Extensions;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Moffstation.Cards.Systems;

// This part handles PlayingCardDeckComponent.
public abstract partial class SharedPlayingCardsSystem
{
    /// The ID of the entity prototype which is used to construct cards dynamically.
    private static readonly EntProtoId<PlayingCardDeckComponent> CardDeckEntId = "PlayingCardDeckDynamic";

    private static readonly Range TopCardRange = ^1..;

    private void InitDeck()
    {
        SubscribeLocalEvent<PlayingCardDeckComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PlayingCardDeckComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PlayingCardDeckComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PlayingCardDeckComponent, PlayingCardStackContentsChangedEvent>(DirtyVisuals);
        SubscribeLocalEvent<PlayingCardDeckComponent, ContainedPlayingCardFlippedEvent>(DirtyVisuals);
        SubscribeLocalEvent<PlayingCardDeckComponent, PlayingCardPickedEvent>(OnPlayingCardPicked);
        SubscribeLocalEvent<PlayingCardDeckComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PlayingCardDeckComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
        SubscribeLocalEvent<PlayingCardDeckComponent, GetVerbsEvent<UtilityVerb>>(OnGetUtilityVerbsStack);
        SubscribeLocalEvent<PlayingCardDeckComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbsDeck);
        SubscribeLocalEvent<PlayingCardDeckComponent, InteractHandEvent>(
            OnInteractHand,
            // ReSharper disable once UseCollectionExpression // Whatever internal stuff is needed for this isn't whitelisted for the sandbox.
            before:
            new[] { typeof(SharedItemSystem) } // We need to run our logic before the generic "pick up items" logic.
        );
    }


    /// <see cref="Take">Takes</see> and returns the top card from <paramref name="entity"/>.
    public Entity<PlayingCardComponent> TakeTopCard(
        Entity<PlayingCardDeckComponent> entity,
        EntityUid user,
        EntityCoordinates? destinationCoords = null
    ) => Take(entity, TopCardRange, destinationCoords ?? Transform(user).Coordinates, user)
        .Single();

    /// <see cref="Transfer">Transfers</see> the top card from <paramref name="source"/> to <paramref name="target"/>
    public void TransferTopCard<TTarget>(
        Entity<PlayingCardDeckComponent> source,
        Entity<TTarget> target,
        EntityUid user
    ) where TTarget : PlayingCardStackComponent => Transfer(source, target, TopCardRange, user);

    /// Creates a new Deck from the given cards. Returns an entity which may be predicted. Spawns on
    /// <paramref name="user"/> if <paramref name="spawnAt"/> is null.
    /// Also returns null in cases where this is not the first time the frame is predicted, to avoid flickering visuals.
    /// <seealso cref="CreateHandPredicted"/>
    public Entity<PlayingCardDeckComponent> CreateDeckPredicted(
        IEnumerable<Entity<PlayingCardComponent>> cards,
        EntityUid user,
        ProtoId<PlayingCardDeckPrototype>? deckPrototype,
        EntityCoordinates? spawnAt = null
    )
    {
        var ent = PredictedCreateStack(CardDeckEntId, spawnAt ?? Transform(user).Coordinates, cards, user);
        ent.Comp.Prototype = deckPrototype;
        return ent;
    }


    private void OnInit(Entity<PlayingCardDeckComponent> entity, ref ComponentInit args)
    {
        // Initialize the contents of the deck from the prototype.
        if (entity.Comp.Prototype is not { } proto ||
            // Don't overwrite existing cards.
            entity.Comp.Cards.Count != 0)
            return;

        // Reverse the cards so that the first in the prototype's list is on the top.
        entity.Comp.Cards = GetCards(proto).Reverse().ToList();
        Dirty(entity);
    }

    private void OnExamined(Entity<PlayingCardDeckComponent> entity, ref ExaminedEvent args)
    {
        if (entity.Comp.TopCard is { } topCardLike &&
            GetComponent(topCardLike) is { FaceDown: false } topCard)
        {
            args.PushMarkup(Loc.GetString(PlayingCardDeckComponent.TopCardExamineLoc, ("card", topCard.ObverseName)));
        }

        OnExamined<PlayingCardDeckComponent>(entity, ref args);
    }

    private void OnInteractHand(Entity<PlayingCardDeckComponent> entity, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        TryDrawToActiveHand(entity, args.User);
        args.Handled = true;
    }


    /// These interactions are invoked when left-clicking on a deck with a held item.
    private void OnInteractUsing(Entity<PlayingCardDeckComponent> targetDeck, ref InteractUsingEvent args)
    {
        if (args.Handled || args.Used == args.Target)
            return;

        var user = args.User;
        args.Handled = HandlePlayingCardComponents(
            args.Used,
            targetDeck,
            // Take top card from this deck, creating a new hand with the used card.
            usedCard => JoinIntoHandIfHeldOtherwiseDeck(usedCard, targetDeck, TopCardRange, user),
            usedStack => TransferTopCard(targetDeck, usedStack, user)
        );
    }

    private void OnGetInteractionVerbs(
        Entity<PlayingCardDeckComponent> entity,
        ref GetVerbsEvent<InteractionVerb> args
    )
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        if (_hands.GetActiveItem(args.User) == null)
        {
            var user = args.User;
            args.Verbs.Add(PlayingCardDeckComponent.Verbs.DrawCard, () => TryDrawToActiveHand(entity, user));
        }
    }

    private void OnGetAlternativeVerbsDeck(
        Entity<PlayingCardDeckComponent> targetDeck,
        ref GetVerbsEvent<AlternativeVerb> args
    )
    {
        OnGetAlternativeVerbsStack(targetDeck, ref args);

        if (!args.CanAccess ||
            !args.CanInteract ||
            args.Hands == null)
            return;

        var user = args.User;

        // Cut deck.
        if (targetDeck.Comp.NumCards > 1)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Act = () => TryCutDeck(targetDeck, user),
                Icon = PlayingCardDeckComponent.Verbs.CutDeck.Icon,
                Text = PlayingCardDeckComponent.Verbs.CutDeck.Text(),
                Disabled = _hands.GetActiveHand(user) is not { } hand ||
                           !_hands.CanPickupToHand(user, targetDeck.Owner, hand),
            });

            // Flip the entire deck, rather than flipping each card's facing direction
            args.Verbs.Add(PlayingCardDeckComponent.Verbs.FlipEntire, () => FlipEntire(targetDeck, user));
        }
    }


    private void FlipEntire(Entity<PlayingCardDeckComponent> deck, EntityUid user)
    {
        deck.Comp.Cards.Reverse();
        deck.Comp.DirtyVisuals = true;
        Dirty(deck);

        foreach (var card in deck.Comp.Cards)
        {
            FlipCardInDeck(card);
        }

        VerbAudioAndPopup(PlayingCardDeckComponent.Verbs.FlipEntire, deck, user);
    }

    /// Tries to draw the top card from <paramref name="entity"/> to <paramref name="user"/>'s active hand. Returns
    /// <c>false</c> if the user cannot pick up the card.
    private bool TryDrawToActiveHand(Entity<PlayingCardDeckComponent> entity, EntityUid user) =>
        PerformIfCanPickUp(user, entity, () => TakeTopCard(entity, user));

    /// Tries to split <paramref name="entity"/> into two roughly equally sized decks, picking up one of the halves.
    private bool TryCutDeck(Entity<PlayingCardDeckComponent> entity, EntityUid user)
    {
        if (!_gameTiming.IsFirstTimePredicted)
            return false;

        return PerformIfCanPickUp(
            user,
            entity,
            () =>
            {
                VerbAudioAndPopup(PlayingCardDeckComponent.Verbs.CutDeck, entity, user);
                var newDeckContents = Take(entity, ^(entity.Comp.NumCards / 2).., Transform(entity).Coordinates, user);
                return CreateDeckPredicted(newDeckContents, user, entity.Comp.Prototype);
            }
        );
    }

    /// Conceptually, this "instantiates" the <see cref="PlayingCardDeckPrototype.Cards">elements</see> in the given
    /// <paramref name="deckId"/>, handling calculating localization strings, sprite layers, etc. Note that this <b>does
    /// not</b> spawn any entities immediately.
    private IEnumerable<PlayingCardInDeck> GetCards(ProtoId<PlayingCardDeckPrototype> deckId)
    {
        if (!_proto.Resolve(deckId, out var deck))
            return [];

        return deck.Cards.SelectMany(deckEl => deckEl switch
        {
            PlayingCardDeckPrototypeElementCard card =>
                Enumerable.Repeat(new PlayingCardInDeckUnspawnedData(card, deck, suit: null), card.Count),
            PlayingCardDeckPrototypeElementPrototypeReference protoRef =>
                Enumerable.Repeat(
                    new PlayingCardInDeckUnspawnedRef(protoRef.Prototype, protoRef.FaceDown),
                    protoRef.Count
                ),
            PlayingCardDeckPrototypeElementSuit s => _proto.Resolve(s.Suit, out var suit)
                ? suit.Cards.SelectMany(suitEl =>
                    Enumerable.Repeat(new PlayingCardInDeckUnspawnedData(suitEl, deck, suit), suitEl.Count)
                )
                : [],
            _ => deckEl.ThrowUnknownInheritor<PlayingCardDeckPrototype.Element, IEnumerable<PlayingCardInDeck>>(),
        });
    }
}
