using System.Linq;
using Content.Shared._Moffstation.Cards.Components;
using Content.Shared._Moffstation.Cards.Events;
using Content.Shared._Moffstation.Cards.Prototypes;
using Content.Shared._Moffstation.Extensions;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using static Content.Shared._Moffstation.Cards.Components.PlayingCardComponent;
using static Content.Shared._Moffstation.Cards.Components.PlayingCardComponent.Verbs;

namespace Content.Shared._Moffstation.Cards.Systems;

// This part handles PlayingCardComponent.
public abstract partial class SharedPlayingCardsSystem
{
    /// The ID of the entity prototype which is used to construct cards dynamically.
    private static readonly EntProtoId<PlayingCardComponent> BaseCardEntId = "PlayingCardDynamic";

    private void InitCard()
    {
        SubscribeLocalEvent<PlayingCardComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PlayingCardComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PlayingCardComponent, PlayingCardFlippedEvent>(OnFlipped);
        SubscribeLocalEvent<PlayingCardComponent, PlayingCardPickedEvent>(OnPlayingCardPicked);
        SubscribeLocalEvent<PlayingCardComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<PlayingCardComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PlayingCardComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<PlayingCardComponent, GetVerbsEvent<UtilityVerb>>(OnGetUtilityVerbs);
    }


    /// Sets the card's facing to <paramref name="faceDown"/>, or flips it if <paramref name="faceDown"/> is null.
    public void Flip(Entity<PlayingCardComponent> card, bool? faceDown)
    {
        // Delegate to internal method, discarding its return value.
        SetFacingOrFlip(card, faceDown);
    }


    private void OnStartup(Entity<PlayingCardComponent> entity, ref ComponentStartup args)
    {
        var ev = new PlayingCardFlippedEvent();
        RaiseLocalEvent(entity, ref ev);
    }

    private void OnExamined(Entity<PlayingCardComponent> entity, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || entity.Comp.FaceDown)
            return;

        args.PushMarkup(Loc.GetString(ExamineText, ("target", entity.Comp.ObverseName)));
    }

    private void OnActivateInWorld(Entity<PlayingCardComponent> entity, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        Flip(entity, faceDown: null);
        args.Handled = true;
    }

    private void OnFlipped(Entity<PlayingCardComponent> entity, ref PlayingCardFlippedEvent args)
    {
        _metadata.SetEntityName(entity, entity.Comp.Name());
        _metadata.SetEntityDescription(
            entity,
            entity.Comp.FaceDown ? entity.Comp.ReverseDescription ?? "" : entity.Comp.Description
        );
        _appearance.SetData(entity, PlayingCardVisuals.IsFaceDown, entity.Comp.FaceDown);
        Dirty(entity);

        var parentUid = Transform(entity).ParentUid;
        if (TryComp<PlayingCardDeckComponent>(parentUid, out var deck))
        {
            deck.DirtyVisuals = true;
        }

        if (TryComp<PlayingCardHandComponent>(parentUid, out var hand))
        {
            hand.DirtyVisuals = true;
        }
    }

    private void OnPlayingCardPicked(Entity<PlayingCardComponent> entity, ref PlayingCardPickedEvent args)
    {
        if (args.Handled)
            return;

        JoinIntoHandIfHeldOtherwiseDeck(entity, TakePickedCard(args, Transform(entity).Coordinates), args.User);
        args.Handled = true;
    }

    /// Creates and returns a new stack made from <paramref name="destination"/> and <paramref cref="moved"/>. See
    /// <see cref="CreateHandIfHeldOtherwiseDeck"/> for more.
    private EntityUid? JoinIntoHandIfHeldOtherwiseDeck(
        Entity<PlayingCardComponent> destination,
        Entity<PlayingCardComponent> moved,
        EntityUid user
    )
    {
        if (CreateHandIfHeldOtherwiseDeck(destination, user) is { } stack)
        {
            Add(stack, moved, user);
        }

        return null;
    }

    /// Creates and returns a new stack made from <paramref name="destination"/> and the cards specifiecd by
    /// <paramref name="range"/> in <paramref name="source"/>. See <see cref="CreateHandIfHeldOtherwiseDeck"/> for more.
    private EntityUid? JoinIntoHandIfHeldOtherwiseDeck<TSource>(
        Entity<PlayingCardComponent> destination,
        Entity<TSource> source,
        Range range,
        Entity<HandsComponent?> user
    ) where TSource : PlayingCardStackComponent
    {
        if (CreateHandIfHeldOtherwiseDeck(destination, user) is { } stack)
        {
            Transfer<TSource, PlayingCardStackComponent>(source, stack, range, user);
        }

        return null;
    }

    /// Creates and returns a degenerate hand or deck from the given <paramref name="card"/>. If <paramref name="user"/>
    /// is holding the card, creates a hand, otherwise creates a deck. Returns null in the case that something goes
    /// wrong with creating a hand.
    /// <remarks>This absolutely should not be used by anything other than <see cref="JoinIntoHandIfHeldOtherwiseDeck"/>
    /// since this makes a degenerate stack and it should always get more cards added to itself!!</remarks>
    private Entity<PlayingCardStackComponent>? CreateHandIfHeldOtherwiseDeck(
        Entity<PlayingCardComponent> card,
        Entity<HandsComponent?> user
    )
    {
        if (!_gameTiming.IsFirstTimePredicted)
            return null;

        // If the destination card's not held, make a deck at its location.
        if (!_hands.IsHolding(user, card, out var handId))
        {
            var deck = CreateDeckPredicted([card], user, null, Transform(card).Coordinates);
            return (deck, deck);
        }

        if (!_hands.TryDrop(user, handId))
            return null;

        var hand = CreateHandPredicted([card], Transform(user).Coordinates, user);
        if (!_hands.TryPickup(user, hand, handId, animate: false, handsComp: user))
        {
            this.AssertOrLogError($"{user} failed to pick up newly spawned hand of cards to expected hand");
        }

        return (hand, hand);
    }


    /// These interactions are invoked when left-clicking on a card with a held item.
    private void OnInteractUsing(Entity<PlayingCardComponent> targetCard, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        args.Handled = HandlePlayingCardComponents(
            args.Used,
            targetCard,
            // Card: pick up this card to the used card, creating a new hand.
            usedCard => JoinIntoHandIfHeldOtherwiseDeck(usedCard, targetCard, user),
            // Deck: pick up this card, placing it on the top of the used stack.
            usedStack => Add(usedStack, targetCard, user)
        );
    }

    /// These verbs are available when right-clicking on an entity while holding a card.
    private void OnGetUtilityVerbs(Entity<PlayingCardComponent> usedCard, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var verbs = args.Verbs;
        var user = args.User;
        HandlePlayingCardComponents(
            args.Target,
            usedCard,
            // Card: pick up the target card to the this card, creating a new hand.
            targetCard => verbs.Add(CardPickup, () => JoinIntoHandIfHeldOtherwiseDeck(usedCard, targetCard, user)),
            // Deck: draw a card from the target deck, creating a new hand with this card.
            targetDeck => verbs.Add(PlayingCardDeckComponent.Verbs.CardPickup,
                () => JoinIntoHandIfHeldOtherwiseDeck(usedCard, targetDeck, TopCardRange, user)),
            // Hand: open the picker UI. It'll handle combining cards and stuff.
            targetHand => verbs.Add(PlayingCardHandComponent.Verbs.CardPickup,
                () => OpenPickerUi(targetHand, usedCard, user))
        );
    }

    /// These verbs are available when right-clicking on a card.
    private void OnGetAlternativeVerbs(Entity<PlayingCardComponent> targetCard, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var verbs = args.Verbs;
        var user = args.User;
        HandlePlayingCardComponents(
            args.Using,
            targetCard,
            // Card: place the used card on top of this card, creating a new deck.
            usedCard => verbs.Add(CardPutDown,
                PlacementVerbPriority,
                () => JoinIntoHandIfHeldOtherwiseDeck(targetCard, usedCard, user)),
            // Stack: draw one card from the used deck, placing it on the target card, creating a new deck.
            usedDeck => verbs.Add(DeckPutDown,
                PlacementVerbPriority,
                () => JoinIntoHandIfHeldOtherwiseDeck(targetCard, usedDeck, TopCardRange, user)),
            // Hand: pick a card from the used hand to combine with the target card.
            usedHand => verbs.Add(HandPutDown, PlacementVerbPriority, () => OpenPickerUi(usedHand, targetCard, user))
        );

        // Flip
        args.Verbs.Add(PlayingCardComponent.Verbs.Flip, () => Flip(targetCard, faceDown: null));
    }


    /// Sets the card's facing to <paramref name="faceDown"/>, or flips it if <paramref name="faceDown"/> is null.
    /// Returns whether or not the card's facing was changed.
    private bool SetFacingOrFlip(Entity<PlayingCardComponent> card, bool? faceDown)
    {
        var didFlip = SetOrInvert(ref card.Comp.FaceDown, faceDown);
        if (didFlip)
        {
            var ev = new PlayingCardFlippedEvent();
            RaiseLocalEvent(card, ref ev);
        }

        return didFlip;
    }

    /// Sets <paramref name="value"/> to <paramref name="setToValue"/>, or inverts it if <paramref name="setToValue"/>
    /// is null. Returns whether or not the value changed.
    private static bool SetOrInvert(ref bool value, bool? setToValue)
    {
        var oldValue = value;
        value = setToValue ?? !oldValue;
        return oldValue != value;
    }

    /// Like <see cref="Flip"/>, but for a <see cref="PlayingCardInDeck"/>, which may be an unspawned card in a
    /// deck.
    private bool FlipCardInDeck(PlayingCardInDeck card, bool? faceDown = null) => card switch
    {
        PlayingCardInDeckNetEnt(var cardNetEnt) =>
            NetEntToCard(cardNetEnt) is { } cardEnt && SetFacingOrFlip(cardEnt, null),
        PlayingCardInDeckUnspawnedData(var data, _, _) => SetOrInvert(ref data.FaceDown, faceDown),
        PlayingCardInDeckUnspawnedRef(_, var fd) => SetOrInvert(ref fd, faceDown),
        _ => card.ThrowUnknownInheritor<PlayingCardInDeck, bool>(),
    };

    /// "Instantiates" <paramref name="data"/> as the returned entity. Returns null if resolving prototypes fails.
    /// Note that the spawned card <b>is predicted on the client</b>.
    private Entity<PlayingCardComponent>? SpawnPredictedDynamicCard(
        PlayingCardInDeckUnspawnedData data,
        EntityCoordinates coords
    )
    {
        var spawned = PredictedSpawnAtPosition(BaseCardEntId, coords);
        var cardComp = Comp<PlayingCardComponent>(spawned);
        TryApplyCardData(ref cardComp, data);
        Dirty(spawned, cardComp);

        var ev = new PlayingCardFlippedEvent();
        RaiseLocalEvent(spawned, ref ev);

        ForceAppearanceUpdate((spawned, cardComp));

        return (spawned, cardComp);
    }

    /// Returns a new, unowned <see cref="PlayingCardComponent"/> with the given <paramref name="data"/>. Returns null
    /// if prototype resolution fails.
    private PlayingCardComponent? ToComponent(PlayingCardInDeckUnspawnedData data)
    {
        var comp = _compFact.GetComponent<PlayingCardComponent>();
        return TryApplyCardData(ref comp, data)
            ? WithFacing(comp, data.Card.FaceDown)
            : null;
    }

    /// Applies the given <paramref name="data"/> to the given <paramref name="comp"/>. Returns whether or not the
    /// component was modified, ie. returns false if prototype resolution failed.
    private bool TryApplyCardData(ref PlayingCardComponent comp, PlayingCardInDeckUnspawnedData data)
    {
        PlayingCardSuitPrototype? suit = null;
        if (!_proto.Resolve(data.Deck, out var deck) ||
            data.Suit is { } suitId &&
            !_proto.Resolve(suitId, out suit))
            return false;

        comp.ObverseLayers = AssembleObverseSpriteLayers(data.Card, deck, suit);
        comp.ReverseLayers = deck.CommonReverseLayers.WithUnlessAlreadySpecified(rsiPath: deck.RsiPath.ToString());
        comp.FaceDown = data.Card.FaceDown;

        (string, object)[] locArgs = suit is null
            ? [("card", Loc.GetString(deck.CardValueLoc, ("card", data.Card.Id.ToLowerInvariant())))]
            :
            [
                ("suit", Loc.GetString(deck.SuitLoc, ("suit", suit.ID.ToLowerInvariant()))),
                ("card", Loc.GetString(deck.CardValueLoc, ("card", data.Card.Id.ToLowerInvariant()))),
            ];

        comp.ObverseName = Loc.GetString(data.Card.NameLoc ?? deck.CardNameLoc, locArgs);
        comp.Description = Loc.GetString(deck.CardDescLoc, locArgs);
        comp.ReverseName = Loc.GetString(deck.CardReverseNameLoc, locArgs);
        comp.ReverseDescription = Loc.GetString(deck.CardReverseDescLoc, locArgs);

        return true;
    }

    private static PrototypeLayerData[] AssembleObverseSpriteLayers(
        PlayingCardDeckPrototypeElementCard card,
        PlayingCardDeckPrototype deck,
        PlayingCardSuitPrototype? suit
    )
    {
        // If neither the card nor the suit specify if the deck's common layers should be used, default to true.
        var layersFromDeck = card.UseDeckLayers ?? suit?.UseDeckLayers ?? true ? deck.CommonObverseLayers : [];
        var layersFromSuit = card.UseSuitLayers ? suit?.CommonObverseLayers ?? [] : [];

        PrototypeLayerData[] layersFromCard;
        if (card.ObverseLayers is { } layers)
        {
            // If there're layers specifically added to this card, replace `{suit}` in the state IDs with the actual suit ID.
            layersFromCard = layers.Select(l =>
                {
                    var layerCopy = l.With();
                    layerCopy.State = layerCopy.State?.Replace("{suit}", suit?.ID.ToLowerInvariant() ?? "");
                    return layerCopy;
                })
                .ToArray();
        }
        else
        {
            var stateFromCard = card.Id.Replace("{suit}", suit?.ID.ToLowerInvariant() ?? "");

            // No layers specified on the card, so assemble the default state ID from the deck, suit and card IDs.
            var stateFromSuit = suit is { DefaultObverseLayerState: { } suitState }
                ? suitState.Replace("{card}", stateFromCard)
                : card.Id;

            var stateFromDeck = deck.DefaultObverseLayerState is { } deckState
                ? deckState.Replace("{card}", stateFromSuit)
                : stateFromSuit;
            layersFromCard = [new PrototypeLayerData { State = stateFromDeck }];
        }

        // Assemble all of the layers together and default the deck's RSI if any layers are missing it.
        return layersFromDeck.Concat(layersFromSuit)
            .Concat(layersFromCard)
            .WithUnlessAlreadySpecified(rsiPath: deck.RsiPath.ToString());
    }

    protected virtual void ForceAppearanceUpdate(Entity<PlayingCardComponent> card)
    {
        // Only does something on the client.
    }
}
