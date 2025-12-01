using System.Numerics;
using Content.Shared._Starlight.PlayingCards;
using Content.Shared._Starlight.PlayingCards.Hand;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.PlayingCards.Card;

public sealed class PlayingCardHandSystem : EntitySystem
{
    [Dependency] private readonly PlayingCardSpriteSystem _cardSpriteSystem = default!;
    
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PlayingCardHandComponent, ComponentStartup>(OnComponentStartupEvent);
        SubscribeNetworkEvent<PlayingCardStackInitiatedEvent>(OnStackStart);
        SubscribeNetworkEvent<PlayingCardStackQuantityChangeEvent>(OnStackUpdate);
    }

    private void UpdateSprite(EntityUid uid, PlayingCardHandComponent comp)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        if (!TryComp(uid, out PlayingCardStackComponent? cardStack))
            return;

        _cardSpriteSystem.TryAdjustLayerQuantity((uid, sprite, cardStack), comp.Limit);

        var cardCount = Math.Min(cardStack.Cards.Count, comp.Limit);

        var intervalAngle = comp.Angle / (cardCount-1);
        var intervalSize = comp.XOffset / (cardCount - 1);

        _cardSpriteSystem.TryHandleLayerConfiguration(
            (uid, sprite, cardStack),
            cardCount,
            (sprt, cardIndex, layerIndex) =>
            {
                var angle = (-(comp.Angle/2)) + cardIndex * intervalAngle;
                var x = (-(comp.XOffset / 2)) + cardIndex * intervalSize;
                var y = -(x * x) + 0.10f;

                sprt.Comp.LayerSetRotation(layerIndex, Angle.FromDegrees(-angle));
                sprt.Comp.LayerSetOffset(layerIndex, new Vector2(x, y));
                sprt.Comp.LayerSetScale(layerIndex, new Vector2(comp.Scale, comp.Scale));
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

    private void OnComponentStartupEvent(EntityUid uid, PlayingCardHandComponent comp, ComponentStartup args) =>
        UpdateSprite(uid, comp);
}