using System.Linq;
using Content.Shared._Starlight.PlayingCards.Card;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.PlayingCards.Hand;

public sealed class PlayingCardHandSystem : EntitySystem
{
    const string PlayingCardHandBaseName = "CardHandBase";
    const string PlayingCardDeckBaseName = "CardDeckBase";

    [Dependency] private readonly PlayingCardStackSystem _cardStack = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PlayingCardComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PlayingCardHandComponent, PlayingCardHandDrawMessage>(OnCardDraw);
        SubscribeLocalEvent<PlayingCardHandComponent, PlayingCardStackQuantityChangeEvent>(OnStackQuantityChange);
        SubscribeLocalEvent<PlayingCardHandComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternativeVerb);
    }

    private void OnStackQuantityChange(EntityUid uid, PlayingCardHandComponent comp,  PlayingCardStackQuantityChangeEvent args)
    {
        if (_net.IsClient)
            return;

        if (!TryComp(uid, out PlayingCardStackComponent? stack))
            return;

        var text = args.Type switch
        {
            PlayingCardStackQuantityChangeType.Added => "cards-stackquantitychange-added",
            PlayingCardStackQuantityChangeType.Removed => "cards-stackquantitychange-removed",
            _ => "cards-stackquantitychange-unknown"
        };

        _popupSystem.PopupEntity(Loc.GetString(text, ("quantity", stack.Cards.Count)), uid);

        _cardStack.FlipAllCards(uid, stack, false);
    }

    private void OnCardDraw(EntityUid uid, PlayingCardHandComponent comp, PlayingCardHandDrawMessage args)
    {
        if (!TryComp(uid, out PlayingCardStackComponent? stack))
            return;
        if (!_cardStack.TryRemoveCard(uid, GetEntity(args.Card), stack))
            return;

        _hands.TryPickupAnyHand(args.Actor, GetEntity(args.Card));


        if (stack.Cards.Count != 1)
            return;
        var lastCard = stack.Cards.Last();
        if (!_cardStack.TryRemoveCard(uid, lastCard, stack))
            return;
        _hands.TryPickupAnyHand(args.Actor, lastCard);
    }

    private void OpenHandMenu(EntityUid user, EntityUid hand)
    {
        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        _ui.OpenUi(hand, CardUiKey.Key, actor.PlayerSession);
    }

    private void OnAlternativeVerb(EntityUid uid, PlayingCardHandComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () => OpenHandMenu(args.User, uid),
            Text = Loc.GetString("cards-verb-pickcard"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/die.svg.192dpi.png")),
            Priority = 3
        });
        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () => ConvertToDeck(args.User, uid),
            Text = Loc.GetString("cards-verb-convert-to-deck"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/rotate_cw.svg.192dpi.png")),
            Priority = 2
        });
    }

    private void OnInteractUsing(EntityUid uid, PlayingCardComponent comp, InteractUsingEvent args)
    {
        if (TryComp(args.Used, out PlayingCardComponent? usedComp) && TryComp(args.Target, out PlayingCardComponent? targetComp))
        {
            TrySetupHandOfCards(args.User, args.Used, usedComp, args.Target, targetComp);
        }
    }

    private void ConvertToDeck(EntityUid user, EntityUid hand)
    {
        if (_net.IsClient)
            return;

        var cardDeck = Spawn(PlayingCardDeckBaseName, Transform(hand).Coordinates);

        bool isHoldingCards = _hands.IsHolding(user, hand);

        EnsureComp<PlayingCardStackComponent>(cardDeck, out var deckStack);
        if (!TryComp(hand, out PlayingCardStackComponent? handStack))
            return;
        _cardStack.TryJoinStacks(cardDeck, hand, deckStack, handStack);

        if (isHoldingCards)
            _hands.TryPickupAnyHand(user, cardDeck);
    }
    
    private void TrySetupHandOfCards(EntityUid user, EntityUid card, PlayingCardComponent comp, EntityUid target, PlayingCardComponent targetComp)
    {
        if (_net.IsClient)
            return;
        var cardHand = Spawn(PlayingCardHandBaseName, Transform(card).Coordinates);
        if (!TryComp(cardHand, out PlayingCardStackComponent? stack))
            return;
        if (!_cardStack.TryInsertCard(cardHand, card, stack) || !_cardStack.TryInsertCard(cardHand, target, stack))
            return;
        if (!_hands.TryPickupAnyHand(user, cardHand))
            return;
        _cardStack.FlipAllCards(cardHand, stack, false);
    }
}