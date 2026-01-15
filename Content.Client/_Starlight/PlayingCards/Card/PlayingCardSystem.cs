using System.Linq;
using Content.Shared._Starlight.PlayingCards;
using Content.Shared._Starlight.PlayingCards.Card;
using Content.Shared._Starlight.PlayingCards.Hand;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;
using PlayingCardHandSystem = Content.Client._Starlight.PlayingCards.Card.PlayingCardHandSystem;

namespace Content.Client._Starlight.PlayingCards.Hand;

public sealed class PlayingCardSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _spriteSystem = default!;
    [Dependency] private readonly PlayingCardHandSystem _cardHand = default!;
    
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PlayingCardComponent, ComponentStartup>(OnComponentStartupEvent);
        SubscribeLocalEvent<PlayingCardComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeNetworkEvent<PlayingCardFlipUpdatedEvent>(OnFlip);
    }

    private void OnComponentStartupEvent(EntityUid uid, PlayingCardComponent comp, ComponentStartup args) =>
        RefreshSprites(uid, comp);

    private void OnHandleState(EntityUid uid, PlayingCardComponent component, AfterAutoHandleStateEvent args)
    {
        RefreshSprites(uid, component);
        var query = EntityQueryEnumerator<PlayingCardStackComponent, PlayingCardHandComponent>();
        while (query.MoveNext(out var stack, out var stackComp, out var handComp))
            foreach (var _ in stackComp.Cards.Where(card => card == uid))
                _cardHand.UpdateSprite(stack, handComp);
    }

    private void OnFlip(PlayingCardFlipUpdatedEvent args)
    {
        if (!TryComp(GetEntity(args.Card), out PlayingCardComponent? comp))
            return;
        UpdateSprite(GetEntity(args.Card), comp);
    }

    private void RefreshSprites(EntityUid uid, PlayingCardComponent comp)
    {
        comp.FrontSpriteLayers?.Clear();
        comp.BackSpriteLayers?.Clear();
        if (comp.Joker)
            comp.FrontSpriteLayers?.Add(new SpriteSpecifier.Rsi(comp.RSIPath, $"sc_joker_{comp.BackFaceName}"));
        else comp.FrontSpriteLayers?.Add(new SpriteSpecifier.Rsi(comp.RSIPath,
            $"sc_{Enum.GetName(comp.Suit)?.ToLower() ?? "spade"}_{comp.Value}_{comp.BackFaceName.ToLower()}"));

        comp.BackSpriteLayers?.Add(new SpriteSpecifier.Rsi(comp.RSIPath, $"{comp.BackFacePrefix}{comp.BackFaceName}"));

        Dirty(uid, comp);
        UpdateSprite(uid, comp);
    }

    private void UpdateSprite(EntityUid uid, PlayingCardComponent comp)
    {
        var newSprite = comp.Flipped ? comp.BackSpriteLayers : comp.FrontSpriteLayers;
        if (newSprite == null)
            return;

        if (!TryComp(uid, out SpriteComponent? spriteComponent))
            return;

        var layerCount = newSprite.Count();

        //inserts Missing Layers
        if (spriteComponent.AllLayers.Count() < layerCount)
        {
            for (var i = spriteComponent.AllLayers.Count(); i < layerCount; i++)
            {
                spriteComponent.AddBlankLayer(i);
            }
        }
        //Removes extra layers
        else if (spriteComponent.AllLayers.Count() > layerCount)
        {
            for (var i = spriteComponent.AllLayers.Count() - 1; i >= layerCount; i--)
            {
                spriteComponent.RemoveLayer(i);
            }
        }

        for (var i = 0; i < newSprite.Count(); i++)
        {
            var layer = newSprite[i];
            spriteComponent.LayerSetSprite(i, layer);
        }
    }
}