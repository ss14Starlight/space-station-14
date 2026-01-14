using System.Linq;
using Content.Shared._Starlight.PlayingCards.Card;
using Content.Shared.Examine;
using Content.Shared.Interaction;
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

    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

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

        EntityManager.DeleteEntity(secondStack);

        RaiseLocalEvent(firstStack, new PlayingCardStackQuantityChangeEvent(GetNetEntity(firstStack), null, PlayingCardStackQuantityChangeType.Joined) );
        RaiseNetworkEvent(new PlayingCardStackQuantityChangeEvent(GetNetEntity(firstStack), null, PlayingCardStackQuantityChangeType.Joined));
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


    private void OnAlternativeVerb(EntityUid uid, PlayingCardStackComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!TryComp(args.Using, out PlayingCardStackComponent? usingStack) ||
            !TryComp(args.Target, out PlayingCardStackComponent? targetStack))
            return;

        if (args.Using == args.Target)
            return;

        args.Verbs.Add(new AlternativeVerb()
        {
            Text = "card-verb-join",
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),
            Priority = 8,
            Act = () => JoinStacks(args.User, args.Target, targetStack, (EntityUid)args.Using, usingStack)
        });
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

    private void InsertCardOnStack(EntityUid user, EntityUid stack, PlayingCardStackComponent stackComponent, EntityUid card)
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
        
        _audio.PlayPredicted(firstComp.PlaceDownSound, Transform(second).Coordinates, user);
        if (_net.IsClient)
            return;

        if (entityUids.Length == 1)
        {
            _storage.PlayPickupAnimation(entityUids.First(), Transform(user).Coordinates, Transform(second).Coordinates, 0);
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
            args.Handled = true;
        }

        // This handles the reverse case, where the user is using a card and inserting it to a stack
        else if (TryComp(args.Target, out PlayingCardStackComponent? stack))
        {
            if (TryComp(args.Used, out PlayingCardComponent? _))
            {
                InsertCardOnStack(args.User, args.Target, stack, args.Used);
                args.Handled = true;
            }
        }
    }
}