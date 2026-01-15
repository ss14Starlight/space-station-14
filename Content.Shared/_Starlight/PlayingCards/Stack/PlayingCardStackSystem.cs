using System.Linq;
using Content.Shared._Starlight.PlayingCards.Card;
using Content.Shared._Starlight.PlayingCards.Deck;
using Content.Shared._Starlight.PlayingCards.Hand;
using Content.Shared.Audio;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.PlayingCards;

/// <summary>
/// System to manage stacks of playing cards
/// Allows for shuffling, flipping, insert, remove, and joining stacks.
/// </summary>
public sealed class PlayingCardStackSystem : EntitySystem
{
    public const string ContainerId = "cardstack-container";
    private readonly string PlayingCardHandBaseName = "CardHandBase";

    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly PlayingCardHandSystem _cardHand = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayingCardStackComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PlayingCardStackComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PlayingCardStackComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<PlayingCardStackComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternativeVerb);
        SubscribeLocalEvent<PlayingCardStackComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<InteractUsingEvent>(OnInteractUsing);
    }
    
    public bool TryRemoveCard(EntityUid uid, EntityUid card, PlayingCardStackComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (!TryComp(card, out PlayingCardComponent? _))
            return false;

        _container.Remove(card, comp.ItemContainer);
        comp.Cards.Remove(card);

        Dirty(uid, comp);

        // Prevents prediction ruining things
        if (_net.IsServer && comp.Cards.Count <= 0)
        {
            EntityManager.QueueDeleteEntity(uid);
        }
        RaiseLocalEvent(uid, new PlayingCardStackQuantityChangeEvent(GetNetEntity(uid), GetNetEntity(card), PlayingCardStackQuantityChangeType.Removed));
        RaiseNetworkEvent(new PlayingCardStackQuantityChangeEvent(GetNetEntity(uid), GetNetEntity(card), PlayingCardStackQuantityChangeType.Removed));
        return true;
    }
    
    public bool TryInsertCard(EntityUid uid, EntityUid card, PlayingCardStackComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (!TryComp(card, out PlayingCardComponent? _))
            return false;

        _container.Insert(card, comp.ItemContainer);
        comp.Cards.Add(card);

        Dirty(uid, comp);
        RaiseLocalEvent(uid, new PlayingCardStackQuantityChangeEvent(GetNetEntity(uid), GetNetEntity(card), PlayingCardStackQuantityChangeType.Added));
        RaiseNetworkEvent(new PlayingCardStackQuantityChangeEvent(GetNetEntity(uid), GetNetEntity(card), PlayingCardStackQuantityChangeType.Added));
        return true;
    }
    
    public bool ShuffleCards(EntityUid uid, PlayingCardStackComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        _random.Shuffle(comp.Cards);

        Dirty(uid, comp);
        RaiseLocalEvent(uid, new PlayingCardStackReorderedEvent(GetNetEntity(uid)));
        RaiseNetworkEvent(new PlayingCardStackReorderedEvent(GetNetEntity(uid)));
        return true;
    }
    
    /// <summary>
    /// Server-Side only method to flip all cards within a stack. This starts CardFlipUpdatedEvent and CardStackFlippedEvent event
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="comp"></param>
    /// <param name="isFlipped">If null, all cards will just invert direction, if it contains a value, then all cards will receive that value</param>
    /// <returns></returns>
    public bool FlipAllCards(EntityUid uid, PlayingCardStackComponent? comp = null, bool? isFlipped = null)
    {
        if (_net.IsClient)
            return false;
        if (!Resolve(uid, ref comp))
            return false;
        foreach (var card in comp.Cards)
        {
            if (!TryComp(card, out PlayingCardComponent? cardComponent))
                continue;

            cardComponent.Flipped = isFlipped?? !cardComponent.Flipped;

            Dirty(card, cardComponent);
            RaiseNetworkEvent(new PlayingCardFlipUpdatedEvent(GetNetEntity(card)));
        }

        RaiseNetworkEvent(new PlayingCardStackFlippedEvent(GetNetEntity(uid)));
        return true;
    }

    public bool FlipCardOrder(EntityUid uid, PlayingCardStackComponent? comp = null)
    {
        if (_net.IsClient)
            return false;
        if (!Resolve(uid, ref comp))
            return false;
        comp.Cards.Reverse();
        foreach (var card in comp.Cards)
        {
            if (!TryComp(card, out PlayingCardComponent? cardComp)) continue;
            cardComp.Flipped = !cardComp.Flipped;
            Dirty(card, cardComp);
        }

        Dirty(uid, comp);
        RaiseNetworkEvent(new PlayingCardStackDeckFlippedEvent(GetNetEntity(uid)));
        return true;
    }

    public bool OrganizeCardsValue(EntityUid uid, PlayingCardStackComponent? comp = null)
    {
        if (_net.IsClient)
            return false;
        if (!Resolve(uid, ref comp))
            return false;
        var flipped = comp.Cards.Count(c => Comp<PlayingCardComponent>(c).Flipped);
        var unflipped = comp.Cards.Count(c => !Comp<PlayingCardComponent>(c).Flipped);
        var reverse = flipped <= unflipped;
        
        comp.Cards = comp.Cards.OrderBy(c => Comp<PlayingCardComponent>(c).Value)
            .ThenByDescending(c => Comp<PlayingCardComponent>(c).Suit)
            .ToList();
        foreach (var card in comp.Cards.ToList().Where(c => Comp<PlayingCardComponent>(c).Joker))
        {
            comp.Cards.Remove(card);
            comp.Cards.Add(card);
        }
        
        if(reverse) comp.Cards.Reverse();
        
        Dirty(uid, comp);
        RaiseNetworkEvent(new PlayingCardStackOrganizedEvent(GetNetEntity(uid)));
        return true;
    }
    
    public bool OrganizeCardsSuit(EntityUid uid, PlayingCardStackComponent? comp = null)
    {
        if (_net.IsClient)
            return false;
        if (!Resolve(uid, ref comp))
            return false;
        var flipped = comp.Cards.Count(c => Comp<PlayingCardComponent>(c).Flipped);
        var unflipped = comp.Cards.Count(c => !Comp<PlayingCardComponent>(c).Flipped);
        var reverse = flipped <= unflipped;
        
        comp.Cards = comp.Cards.OrderByDescending(c => Comp<PlayingCardComponent>(c).Suit)
            .ThenBy(c => Comp<PlayingCardComponent>(c).Value)
            .ToList();
        foreach (var card in comp.Cards.ToList().Where(c => Comp<PlayingCardComponent>(c).Joker))
        {
            comp.Cards.Remove(card);
            comp.Cards.Insert(0, card);
        }
        
        if(reverse) comp.Cards.Reverse();
        
        Dirty(uid, comp);
        RaiseNetworkEvent(new PlayingCardStackOrganizedEvent(GetNetEntity(uid)));
        return true;
    }
    
    public bool TryJoinStacks(EntityUid firstStack, EntityUid secondStack, PlayingCardStackComponent? firstComp = null, PlayingCardStackComponent? secondComp = null)
    {
        if (firstStack == secondStack)
            return false;
        if (!Resolve(firstStack, ref firstComp) || !Resolve(secondStack, ref secondComp))
            return false;

        foreach (var card in secondComp.Cards.ToList())
        {
            _container.Remove(card, secondComp.ItemContainer);
            secondComp.Cards.Remove(card);
            firstComp.Cards.Add(card);
            _container.Insert(card, firstComp.ItemContainer);
        }
        Dirty(firstStack, firstComp);

        EntityManager.QueueDeleteEntity(secondStack);

        RaiseLocalEvent(firstStack, new PlayingCardStackQuantityChangeEvent(GetNetEntity(firstStack), null, PlayingCardStackQuantityChangeType.Added) );
        RaiseNetworkEvent(new PlayingCardStackQuantityChangeEvent(GetNetEntity(firstStack), null, PlayingCardStackQuantityChangeType.Added));
        return true;
    }
    
    private void OnStartup(EntityUid uid, PlayingCardStackComponent component, ComponentStartup args)
    {
        component.ItemContainer = _container.EnsureContainer<Container>(uid, ContainerId);
    }

    private void OnMapInit(EntityUid uid, PlayingCardStackComponent comp, MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        var coordinates = Transform(uid).Coordinates;
        foreach (var ent in comp.Content.Select(id => Spawn(id, coordinates)).Where(ent => !TryInsertCard(uid, ent, comp)))
        {
            Log.Error($"Entity {ToPrettyString(ent)} was unable to be initialized into stack {ToPrettyString(uid)}");
            return;
        }
        RaiseNetworkEvent(new PlayingCardStackInitiatedEvent(GetNetEntity(uid)));
    }


    // It seems the cards don't get removed if this event is not subscribed... strange right? thanks again bin system
    private void
        OnEntRemoved(EntityUid uid, PlayingCardStackComponent component, EntRemovedFromContainerMessage args) =>
        component.Cards.Remove(args.Entity);

    private void OnExamine(EntityUid uid, PlayingCardStackComponent component, ExaminedEvent args) =>
        args.PushText(Loc.GetString("card-stack-examine", ("count", component.Cards.Count)));


    private void OnAlternativeVerb(EntityUid uid, PlayingCardStackComponent component,
        GetVerbsEvent<AlternativeVerb> args)
    {
        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () => TryShuffle(uid, component, args.User),
            Text = Loc.GetString("cards-verb-shuffle"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/die.svg.192dpi.png")),
            Priority = 6
        });
        if(!HasComp<PlayingCardHandComponent>(args.Target))
        {
            args.Verbs.Add(new AlternativeVerb()
            {
                Act = () => TryFlipCards(uid, component, args.User, false),
                Text = Loc.GetString(component.FlipCardsUpLocId),
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/flip.svg.192dpi.png")),
                Priority = 4
            });
            args.Verbs.Add(new AlternativeVerb()
            {
                Act = () => TryFlipCards(uid, component, args.User, true),
                Text = Loc.GetString(component.FlipCardsDownLocId),
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/flip.svg.192dpi.png")),
                Priority = 3
            });
            args.Verbs.Add(new AlternativeVerb()
            {
                Act = () => TryFlipDeck(uid, component, args.User),
                Text = Loc.GetString(component.FlipDeckLocId),
                Icon = new SpriteSpecifier.Texture(
                    new ResPath("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),
                Priority = 2
            });
        }
        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () => TryOrganize(uid, component, args.User, OrganizeType.Value),
            Text = Loc.GetString(component.OrganizeValueLocId),
            Icon = new SpriteSpecifier.Texture(
                new ResPath("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),
            Priority = 1
        });
        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () => TryOrganize(uid, component, args.User, OrganizeType.Suit),
            Text = Loc.GetString(component.OrganizeSuitLocId),
            Icon = new SpriteSpecifier.Texture(
                new ResPath("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),
            Priority = 0
        });
        if (!HasComp<PlayingCardDeckComponent>(uid) && HasComp<PlayingCardHandComponent>(args.Using))
            args.Verbs.Add(new AlternativeVerb()
            {
                Act = () => TrySplit(args.Target, component, args.User),
                Text = Loc.GetString("cards-verb-split"),
                Icon = new SpriteSpecifier.Texture(
                    new ResPath("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
                Priority = 7
            });
        if(args.Using != args.Target && args.Using is not null && TryComp<PlayingCardStackComponent>(args.Using, out var stack))
            args.Verbs.Add(new AlternativeVerb()
            {
                Text = Loc.GetString(component.JoinCardsLocId),
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),
                Priority = 8,
                Act = () => JoinStacks(args.User, args.Target, component, (EntityUid)args.Using, stack)
            });
    }

    private void TryShuffle(EntityUid deck, PlayingCardStackComponent stack, EntityUid user)
    {
        ShuffleCards(deck, stack);
        if (_net.IsClient)
            return;

        _audio.PlayPvs(stack.ShuffleSound, deck, AudioHelpers.WithVariation(0.05f, _random));
        _popup.PopupEntity(Loc.GetString("card-verb-shuffle-success", ("user", MetaData(user).EntityName)), deck);
    }

    private void TryFlipCards(EntityUid deck, PlayingCardStackComponent stack, EntityUid user, bool isFlipped)
    {
        if (_net.IsClient)
            return;
        FlipAllCards(deck, stack, isFlipped: isFlipped);
        var locId = isFlipped ? stack.FlipCardsDownLocId : stack.FlipCardsUpLocId;
        _audio.PlayPvs(stack.ShuffleSound, deck, AudioHelpers.WithVariation(0.05f, _random));
        _popup.PopupEntity(
            Loc.GetString($"{string.Join('-', locId.Id.Split('-').ToList().SkipLast(1))}-{stack.PopupSuccessSuffix}",
            [("user", MetaData(user).EntityName),("direction", isFlipped ? "downward" : "upward")]), deck);
    }

    private void TryFlipDeck(EntityUid uid, PlayingCardStackComponent stack,
        EntityUid user)
    {
        if (_net.IsClient) return;
        if (!FlipCardOrder(uid, stack)) return;
        _audio.PlayPvs(stack.PlaceDownSound, uid, AudioHelpers.WithVariation(0.05f, _random));
        _popup.PopupEntity(
            Loc.GetString($"{stack.FlipDeckLocId}-{stack.PopupSuccessSuffix}", ("user", MetaData(user).EntityName)), uid);
    }

    private void TryOrganize(EntityUid uid, PlayingCardStackComponent stack,
        EntityUid user, OrganizeType type)
    {
        if (_net.IsClient) return;
        if (type is OrganizeType.Value)
        {
            if (!OrganizeCardsValue(uid, stack)) return;
        }
        else if (!OrganizeCardsSuit(uid, stack)) return;
        var locId = type is OrganizeType.Value ? stack.OrganizeValueLocId : stack.OrganizeSuitLocId;
        _audio.PlayPvs(stack.ShuffleSound, uid, AudioHelpers.WithVariation(0.05f, _random));
        _popup.PopupEntity(
            Loc.GetString($"{string.Join('-', locId.Id.Split('-').ToList().SkipLast(1))}-{stack.PopupSuccessSuffix}",
                [("user", MetaData(user).EntityName), ("type", locId.Id.Split('-').Last())]),
            uid);
    }
    
    private void TrySplit(EntityUid uid, PlayingCardStackComponent stack, EntityUid user)
    {
        _audio.PlayPredicted(stack.PickUpSound, Transform(uid).Coordinates, user);

        if (!_net.IsServer || stack.Cards.Count <= 1)
            return;

        var cardDeck = Spawn(PlayingCardHandBaseName, Transform(uid).Coordinates);

        EnsureComp<PlayingCardStackComponent>(cardDeck, out var deckStack);

        TransferNLastCardFromStacks(user, stack.Cards.Count / 2, uid, stack, cardDeck, deckStack);

        _hands.TryPickupAnyHand(user, cardDeck);
    }
    
    private void JoinStacks(EntityUid user, EntityUid first, PlayingCardStackComponent firstComp, EntityUid second, PlayingCardStackComponent secondComp)
    {
        _audio.PlayPredicted(firstComp.PlaceDownSound, Transform(second).Coordinates, user);
        if (_net.IsServer)
        {
            _storage.PlayPickupAnimation(first, Transform(user).Coordinates, Transform(second).Coordinates, 0);
            TryJoinStacks(first, second, firstComp, secondComp);
        }
    }

    public void InsertCardOnStack(EntityUid user, EntityUid stack, PlayingCardStackComponent stackComponent, EntityUid card)
    {
        if (!TryInsertCard(stack, card))
            return;

        _audio.PlayPredicted(stackComponent.PlaceDownSound, Transform(stack).Coordinates, user);
        if (_net.IsClient)
            return;
        _storage.PlayPickupAnimation(card, Transform(user).Coordinates, Transform(stack).Coordinates, 0);
    }
    
    /// <summary>
    /// This takes the last card from the first stack and inserts it into the second stack
    /// </summary>
    public void TransferNLastCardFromStacks(EntityUid user, int n, EntityUid first, PlayingCardStackComponent firstComp, EntityUid second, PlayingCardStackComponent secondComp)
    {
        if (firstComp.Cards.Count <= 0)
            return;
        
        var cards = firstComp.Cards.TakeLast(n);

        var entityUids = cards as EntityUid[] ?? cards.ToArray();
        foreach (var card in entityUids)
        {
            if (!TryRemoveCard(first, card))
                return;

            if (!TryInsertCard(second, card))
                return;
        }
        
        _audio.PlayPredicted(firstComp.PlaceDownSound, Transform(first).Coordinates, user);
        if (_net.IsClient)
            return;

        if (entityUids.Length == 1)
        {
            _storage.PlayPickupAnimation(entityUids.First(), Transform(first).Coordinates, Transform(second).Coordinates, 0);
        }
        else
        {
            _storage.PlayPickupAnimation(first, Transform(first).Coordinates, Transform(second).Coordinates, 0);
        }
    }

    private void OnInteractUsing(InteractUsingEvent args)
    {
        if (args.Handled)
            return;
        
        // This checks if the user is using an item with Stack component
        if (TryComp(args.Used, out PlayingCardStackComponent? usedStack))
        {
            // If the target is a card, then it will insert the card into the stack
            if (TryComp(args.Target, out PlayingCardComponent? _))
            {
                InsertCardOnStack(args.User, args.Used, usedStack, args.Target);
                args.Handled = true;
                return;

            }

            // If instead, the target is a stack, then it will join the two stacks
            if (!TryComp(args.Target, out PlayingCardStackComponent? targetStack))
                return;
            TransferNLastCardFromStacks(args.User, 1, args.Target, targetStack, args.Used, usedStack);
            if (targetStack.Cards.Count <= 1)
                _cardHand.TryDestroyHandOfCards(args.User, args.Target, out _);
            args.Handled = true;
        }

        else if (TryComp<PlayingCardComponent>(args.Used, out var card))
        {
            if (!TryComp<PlayingCardStackComponent>(args.Target, out var targetStack)) return;
            _audio.PlayPredicted(targetStack.PlaceDownSound, Transform(args.Target).Coordinates, args.User);
            _cardHand.TrySetupHandOfCards(args.User, args.Used, card, args.Target, targetStack, out var result);
            if (!TryComp<PlayingCardStackComponent>(result, out var newStack)) return;
            _storage.PlayPickupAnimation(newStack.Cards.Last(), Transform(args.Target).Coordinates, Transform(args.User).Coordinates, 0);
        }

        // handled in altverb in decksystem
        // // This handles the reverse case, where the user is using a card and inserting it to a stack
        // else if (TryComp(args.Target, out PlayingCardStackComponent? stack))
        // {
        //     if (TryComp(args.Used, out PlayingCardComponent? _))
        //     {
        //         InsertCardOnStack(args.User, args.Target, stack, args.Used);
        //         args.Handled = true;
        //     }
        // }
    }
    
    private enum OrganizeType
    {
        Value,
        Suit
    }
}