using System.Numerics;
using Content.Shared._Starlight.PlayingCards;
using Content.Shared._Starlight.PlayingCards.Hand;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.PlayingCards.Card;

public sealed class PlayingCardHandSystem : EntitySystem
{
    [Dependency] private readonly PlayingCardSpriteSystem _cardSpriteSystem = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PlayingCardHandComponent, ComponentStartup>(OnComponentStartupEvent);
        SubscribeNetworkEvent<PlayingCardStackInitiatedEvent>(OnStackStart);
        SubscribeNetworkEvent<PlayingCardStackQuantityChangeEvent>(OnStackUpdate);
        SubscribeNetworkEvent<PlayingCardStackReorderedEvent>(OnReorder);
        SubscribeNetworkEvent<PlayingCardStackOrganizedEvent>(OnOrganized);
        SubscribeNetworkEvent<PlayingCardStackFlippedEvent>(OnStackFlip);
        SubscribeNetworkEvent<PlayingCardStackDeckFlippedEvent>(OnDeckFlip);
    }

    public void UpdateSprite(EntityUid uid, PlayingCardHandComponent comp)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        if (!TryComp(uid, out PlayingCardStackComponent? cardStack))
            return;

        _cardSpriteSystem.TryAdjustLayerQuantity((uid, sprite, cardStack), comp.Limit);

        var cardCount = Math.Min(cardStack.Cards.Count, comp.Limit);

        var intervalAngle = comp.Angle / (cardCount - 1);
        var intervalSize = comp.XOffset / (cardCount - 1);

        // literally just to prevent it from not rendering outright if it somehow doesn't convert to a single card
        if (intervalAngle == 0 || intervalSize == 0)
        {
            intervalAngle = 0.01f;
            intervalSize = 0.01f;
        }

        _cardSpriteSystem.TryHandleLayerConfiguration(
            (uid, sprite, cardStack),
            cardCount,
            (_, cardIndex, layerIndex) =>
            {
                var angle = -(comp.Angle/2) + (cardIndex * intervalAngle);
                var x = -(comp.XOffset / 2) + (cardIndex * intervalSize);
                var y = -(x * x) + 0.10f;

                _sprite.LayerSetRotation(uid, layerIndex, Angle.FromDegrees(-angle));
                _sprite.LayerSetOffset(uid, layerIndex, new Vector2(x, y));
                _sprite.LayerSetScale(uid, layerIndex, new Vector2(comp.Scale, comp.Scale));
                return true;
            }
        );
    }

    private void OnStackUpdate(PlayingCardStackQuantityChangeEvent args)
    {
        if (!TryComp(GetEntity(args.CardStack), out PlayingCardHandComponent? comp))
            return;
        UpdateSprite(GetEntity(args.CardStack), comp);
    }

    private void OnStackStart(PlayingCardStackInitiatedEvent args)
    {
        var entity = GetEntity(args.CardStack);
        if (!TryComp(entity, out PlayingCardHandComponent? comp))
            return;

        UpdateSprite(entity, comp);
    }
    
    private void OnStackFlip(PlayingCardStackFlippedEvent args)
    {
        if (!TryComp(GetEntity(args.CardStack), out PlayingCardHandComponent? comp))
            return;
        UpdateSprite(GetEntity(args.CardStack), comp);
    }
    
    private void OnDeckFlip(PlayingCardStackDeckFlippedEvent args)
    {
        if (!TryComp(GetEntity(args.CardStack), out PlayingCardHandComponent? comp))
            return;
        UpdateSprite(GetEntity(args.CardStack), comp);
    }

    private void OnReorder(PlayingCardStackReorderedEvent args)
    {
        if (!TryComp(GetEntity(args.CardStack), out PlayingCardHandComponent? comp))
            return;
        UpdateSprite(GetEntity(args.CardStack), comp);
    }

    private void OnOrganized(PlayingCardStackOrganizedEvent args)
    {
        if (!TryComp(GetEntity(args.CardStack), out PlayingCardHandComponent? comp))
            return;
        UpdateSprite(GetEntity(args.CardStack), comp);
    }

    private void OnComponentStartupEvent(EntityUid uid, PlayingCardHandComponent comp, ComponentStartup args) =>
        UpdateSprite(uid, comp);
}