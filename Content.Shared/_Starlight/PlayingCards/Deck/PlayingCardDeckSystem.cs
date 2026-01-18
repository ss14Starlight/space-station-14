using Content.Shared._Starlight.PlayingCards.Card;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.PlayingCards.Deck;

public sealed class PlayingCardDeckSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PlayingCardStackSystem _cardStackSystem = default!;
    [Dependency] private readonly INetManager _net = default!;

    private const string PlayingCardDeckBaseName = "CardDeckBase";
    private const string PlayingCardHandBaseName = "CardHandBase";

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayingCardDeckComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<PlayingCardDeckComponent, GetVerbsEvent<AlternativeVerb>>(AddTurnOnVerb);
    }
    
    private void AddTurnOnVerb(EntityUid uid, PlayingCardDeckComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        if (!TryComp(uid, out PlayingCardStackComponent? comp))
            return;

        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () =>
            {
                if (_hands.TryGetActiveItem((args.User, args.Hands), out var item) &&
                    TryComp<PlayingCardComponent>(item, out var card))
                {
                    _cardStackSystem.InsertCardOnStack(args.User, args.Target, comp, item.Value);
                    return;
                }
                TrySplit(args.Target, component, comp, args.User);
            },
            Text = Loc.GetString("cards-verb-split"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Priority = 7
        });
        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () => ConvertToHand(args.User, uid),
            Text = Loc.GetString("cards-verb-convert-to-hand"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/rotate_cw.svg.192dpi.png")),
            Priority = 5
        });
        
    }

    private void TrySplit(EntityUid uid, PlayingCardDeckComponent deck, PlayingCardStackComponent stack, EntityUid user)
    {
        _audio.PlayPredicted(deck.PickUpSound, Transform(uid).Coordinates, user);

        if (!_net.IsServer || stack.Cards.Count <= 1)
            return;

        var cardDeck = Spawn(PlayingCardDeckBaseName, Transform(uid).Coordinates);

        EnsureComp<PlayingCardStackComponent>(cardDeck, out var deckStack);

        _cardStackSystem.TransferNLastCardFromStacks(user, stack.Cards.Count / 2, uid, stack, cardDeck, deckStack);

        _hands.TryPickupAnyHand(user, cardDeck);
    }

    private void OnInteractHand(EntityUid uid, PlayingCardDeckComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(uid, out PlayingCardStackComponent? comp))
            return;

        if (comp.Cards.Count <= 0)
            return;

        if (!comp.Cards.TryGetValue(comp.Cards.Count-1, out var card))
            return;

        if (!_cardStackSystem.TryRemoveCard(uid, card, comp))
            return;

        _hands.TryPickupAnyHand(args.User, card);

        _audio.PlayPredicted(component.PickUpSound, Transform(uid).Coordinates, args.User);

        args.Handled = true;
    }
    
    private void ConvertToHand(EntityUid user, EntityUid deck)
    {
        if (_net.IsClient)
            return;

        var cardDeck = Spawn(PlayingCardHandBaseName, Transform(deck).Coordinates);

        var isHoldingCards = _hands.IsHolding(user, deck);

        if (!TryComp(cardDeck, out PlayingCardStackComponent? deckStack))
            return;
        if (!TryComp(deck, out PlayingCardStackComponent? handStack))
            return;
        if (TryComp<HandsComponent>(user, out var hands)) _hands.TryDrop((user, hands), deck);
        _cardStackSystem.TryJoinStacks(cardDeck, deck, deckStack, handStack);

        if (isHoldingCards)
            _hands.TryPickupAnyHand(user, cardDeck);
    }
}