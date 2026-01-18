using System.Linq;
using System.Numerics;
using Content.Shared._Starlight.PlayingCards;
using Content.Shared._Starlight.PlayingCards.Deck;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.PlayingCards.Deck;

public sealed class PlayingCardDeckSystem : EntitySystem
{
    private readonly Dictionary<Entity<PlayingCardDeckComponent>, int> _notInitialized = [];
    [Dependency] private readonly PlayingCardSpriteSystem _cardSpriteSystem = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    
    /// <inheritdoc/>
    public override void Initialize()
    {
        UpdatesOutsidePrediction = false;
        SubscribeLocalEvent<PlayingCardDeckComponent, ComponentStartup>(OnComponentStartupEvent);
        SubscribeNetworkEvent<PlayingCardStackInitiatedEvent>(OnStackStart);
        SubscribeNetworkEvent<PlayingCardStackQuantityChangeEvent>(OnStackUpdate);
        SubscribeNetworkEvent<PlayingCardStackReorderedEvent>(OnReorder);
        SubscribeNetworkEvent<PlayingCardStackOrganizedEvent>(OnOrganized);
        SubscribeNetworkEvent<PlayingCardStackFlippedEvent>(OnStackFlip);
        SubscribeNetworkEvent<PlayingCardStackDeckFlippedEvent>(OnDeckFlip);
        SubscribeLocalEvent<PlayingCardDeckComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Lazy way to make sure the sprite starts correctly
        foreach (var kv in _notInitialized.ToArray())
        {
            var ent = kv.Key;

            if (kv.Value >= 5)
            {
                _notInitialized.Remove(ent);
                continue;
            }

            _notInitialized[ent] = kv.Value + 1;

            if (!TryComp(ent.Owner, out PlayingCardStackComponent? stack) || stack.Cards.Count <= 0)
                continue;
            
            // If the card was STILL not initialized, we skip it
            if (!TryGetCardLayer(stack.Cards.Last(), out var _))
                continue;

            // If cards were correctly initialized, we update the sprite
            UpdateSprite(ent.Owner, ent.Comp);
            _notInitialized.Remove(ent);
        }
    }

    private bool TryGetCardLayer(EntityUid card, out SpriteComponent.Layer? layer)
    {
        layer = null;
        if (!TryComp(card, out SpriteComponent? cardSprite))
            return false;

        if (!_sprite.TryGetLayer((card, cardSprite), 0, out var l, false))
            return false;

        layer = l;
        return true;
    }

    private void UpdateSprite(EntityUid uid, PlayingCardDeckComponent comp)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        if (!TryComp(uid, out PlayingCardStackComponent? cardStack))
            return;
        
        // Prevents error appearing at spawnMenu
        if (cardStack.Cards.Count <= 0 || !TryGetCardLayer(cardStack.Cards.Last(), out var cardlayer) ||
            cardlayer == null)
        {
            _notInitialized[(uid, comp)] = 0;
            return;
        }

        _cardSpriteSystem.TryAdjustLayerQuantity((uid, sprite, cardStack), comp.CardLimit);

        _cardSpriteSystem.TryHandleLayerConfiguration(
            (uid, sprite, cardStack),
            comp.CardLimit,
            (_, cardIndex, layerIndex) =>
            {
                _sprite.LayerSetRotation(uid, layerIndex, Angle.FromDegrees(90));
                _sprite.LayerSetOffset(uid, layerIndex, new Vector2(0, (comp.YOffset * cardIndex)));
                _sprite.LayerSetScale(uid, layerIndex, new Vector2(comp.Scale, comp.Scale));
                return true;
            }
        );
    }

    private void OnStackUpdate(PlayingCardStackQuantityChangeEvent args)
    {
        if (!TryComp(GetEntity(args.CardStack), out PlayingCardDeckComponent? comp))
            return;
        UpdateSprite(GetEntity(args.CardStack), comp);
    }

    private void OnStackFlip(PlayingCardStackFlippedEvent args)
    {
        if (!TryComp(GetEntity(args.CardStack), out PlayingCardDeckComponent? comp))
            return;
        UpdateSprite(GetEntity(args.CardStack), comp);
    }
    
    private void OnDeckFlip(PlayingCardStackDeckFlippedEvent args)
    {
        if (!TryComp(GetEntity(args.CardStack), out PlayingCardDeckComponent? comp))
            return;
        UpdateSprite(GetEntity(args.CardStack), comp);
    }

    private void OnReorder(PlayingCardStackReorderedEvent args)
    {
        if (!TryComp(GetEntity(args.CardStack), out PlayingCardDeckComponent? comp))
            return;
        UpdateSprite(GetEntity(args.CardStack), comp);
    }
    
    private void OnOrganized(PlayingCardStackOrganizedEvent args)
    {
        if (!TryComp(GetEntity(args.CardStack), out PlayingCardDeckComponent? comp))
            return;
        UpdateSprite(GetEntity(args.CardStack), comp);
    }

    private void OnAppearanceChanged(EntityUid uid, PlayingCardDeckComponent comp, AppearanceChangeEvent args) => UpdateSprite(uid, comp);
    private void OnComponentStartupEvent(EntityUid uid, PlayingCardDeckComponent comp, ComponentStartup args) => UpdateSprite(uid, comp);

    private void OnStackStart(PlayingCardStackInitiatedEvent args)
    {
        var entity = GetEntity(args.CardStack);
        if (!TryComp(entity, out PlayingCardDeckComponent? comp))
            return;

        UpdateSprite(entity, comp);
    }
}