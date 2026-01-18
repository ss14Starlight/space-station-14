using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Starlight.PlayingCards.Card;
using Content.Shared.Hands.Components;
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
    }

    private void OnCardDraw(EntityUid uid, PlayingCardHandComponent comp, PlayingCardHandDrawMessage args)
    {
        if (!TryComp(uid, out PlayingCardStackComponent? stack))
            return;
        if (!_cardStack.TryRemoveCard(uid, GetEntity(args.Card), stack))
            return;

        if (TryComp<HandsComponent>(args.Actor, out var hands))
        {
            if (_hands.TryGetActiveItem((args.Actor, hands), out var item))
            {
                if (TryComp<PlayingCardStackComponent>(item, out var targetStack))
                    _cardStack.TryInsertCard(item.Value, GetEntity(args.Card), targetStack);
                else if (TryComp<PlayingCardComponent>(item, out var card))
                    TrySetupHandOfCards(args.Actor, GetEntity(args.Card), card, item.Value, out _);
                else _hands.TryPickupAnyHand(args.Actor, GetEntity(args.Card));
            }
            else _hands.TryPickupAnyHand(args.Actor, GetEntity(args.Card));
        }
        else _hands.TryPickupAnyHand(args.Actor, GetEntity(args.Card));

        if (stack.Cards.Count != 1)
            return;
        TryDestroyHandOfCards(args.Actor, uid, out _);
    }

    private void OpenHandMenu(EntityUid user, EntityUid hand)
    {
        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        _ui.OpenUi(hand, CardUiKey.Key, actor.PlayerSession);
    }

    private void OnAlternativeVerb(EntityUid uid, PlayingCardHandComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !args.CanComplexInteract) return;
        
        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () =>
            {
                if (!TryComp<PlayingCardStackComponent>(uid, out var stack)) return;
                var flipped = stack.Cards.Count(c => Comp<PlayingCardComponent>(c).Flipped);
                var unflipped = stack.Cards.Count(c => !Comp<PlayingCardComponent>(c).Flipped);
                if(flipped == 0) _cardStack.FlipAllCards(uid, stack, true);
                else if(unflipped == 0) _cardStack.FlipAllCards(uid, stack, false);
                else _cardStack.FlipAllCards(uid, stack, flipped >= unflipped);
            },
            Text = Loc.GetString("cards-verb-flip-toggle"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),
            Priority = 12
        });
        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () =>
            {
                if (!TryComp<PlayingCardStackComponent>(uid, out var stack)) return;
                _cardStack.FlipAllCards(uid, stack, false);
            },
            Text = Loc.GetString("cards-verb-flip-all-up"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/flip.svg.192dpi.png")),
            Priority = 11
        });
        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () =>
            {
                if (!TryComp<PlayingCardStackComponent>(uid, out var stack)) return;
                _cardStack.FlipAllCards(uid, stack, true);
            },
            Text = Loc.GetString("cards-verb-flip-all-down"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/flip.svg.192dpi.png")),
            Priority = 10
        });
        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () => OpenHandMenu(args.User, uid),
            Text = Loc.GetString("cards-verb-pickcard"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/die.svg.192dpi.png")),
            Priority = 6
        });
        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () => ConvertToDeck(args.User, uid),
            Text = Loc.GetString("cards-verb-convert-to-deck"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/rotate_cw.svg.192dpi.png")),
            Priority = 5
        });
    }

    private void OnInteractUsing(EntityUid uid, PlayingCardComponent comp, InteractUsingEvent args)
    {
        if (TryComp(args.Used, out PlayingCardComponent? usedComp) && TryComp(args.Target, out PlayingCardComponent? targetComp))
            TrySetupHandOfCards(args.User, args.Used, usedComp, args.Target, out _);
    }

    private void ConvertToDeck(EntityUid user, EntityUid hand)
    {
        if (_net.IsClient)
            return;

        var cardDeck = Spawn(PlayingCardDeckBaseName, Transform(hand).Coordinates);

        var isHoldingCards = _hands.IsHolding(user, hand);
        
        if (!TryComp(cardDeck, out PlayingCardStackComponent? deckStack))
            return;
        if (!TryComp(hand, out PlayingCardStackComponent? handStack))
            return;
        if (TryComp<HandsComponent>(user, out var hands)) _hands.TryDrop((user, hands), hand);
        _cardStack.TryJoinStacks(cardDeck, hand, deckStack, handStack);

        if (isHoldingCards)
            _hands.TryPickupAnyHand(user, cardDeck);
    }

    public bool TrySetupHandOfCards(EntityUid user, EntityUid card, PlayingCardComponent comp, EntityUid target, out EntityUid? result)
    {
        result = null;
        if (_net.IsClient)
            return false;
        var cardHand = Spawn(PlayingCardHandBaseName, Transform(card).Coordinates);
        if (!TryComp(cardHand, out PlayingCardStackComponent? stack))
            return false;
        if (!_cardStack.TryInsertCard(cardHand, card, stack) || !_cardStack.TryInsertCard(cardHand, target, stack))
            return false;
        if (!_hands.TryPickupAnyHand(user, cardHand))
            return false;
        result = cardHand;
        return true;
    }

    public bool TrySetupHandOfCards(EntityUid user, EntityUid card, PlayingCardComponent comp, EntityUid target,
        PlayingCardStackComponent targetStack, [NotNullWhen(true)] out EntityUid? result)
    {
        result = null;
        if (_net.IsClient) return false;
        var cardHand = Spawn(PlayingCardHandBaseName, Transform(card).Coordinates);
        if (!TryComp<PlayingCardStackComponent>(cardHand, out var stack)) return false;
        if (!_cardStack.TryInsertCard(cardHand, card, stack)) return false;
        _cardStack.TransferNLastCardFromStacks(user, 1, target, targetStack, cardHand, stack);
        if (!_hands.TryPickupAnyHand(user, cardHand))
            return false;
        result = cardHand;
        return true;
    }

    public bool TryDestroyHandOfCards(EntityUid user, EntityUid cardHand, out EntityUid? result)
    {
        result = null;
        if (_net.IsClient) return false;
        if(!TryComp<PlayingCardStackComponent>(cardHand, out var stack)) return false;
        if (!TryComp<HandsComponent>(user, out var hands)) return false;
        var target = stack.Cards.First();
        _cardStack.TryRemoveCard(cardHand, target, stack);
        _hands.TryDrop((user, hands), cardHand);
        _hands.TryPickupAnyHand(user, target);
        QueueDel(cardHand);
        result = target;
        return true;
    }
}