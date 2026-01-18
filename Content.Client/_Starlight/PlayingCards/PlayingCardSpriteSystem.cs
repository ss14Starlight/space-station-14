using System.Linq;
using Content.Shared._Starlight.PlayingCards;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.PlayingCards;

public sealed class PlayingCardSpriteSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    
    public bool TryAdjustLayerQuantity(Entity<SpriteComponent, PlayingCardStackComponent> uid, int? cardLimit = null)
    {
        var sprite = uid.Comp1;
        var stack = uid.Comp2;
        var cardCount = cardLimit == null ? stack.Cards.Count : Math.Min(stack.Cards.Count, cardLimit.Value);

        var layerCount = 0;
        //Gets the quantity of layers
        foreach (var card in stack.Cards.TakeLast(cardCount))
        {
            if (!TryComp(card, out SpriteComponent? cardSprite))
                return false;

            layerCount += cardSprite.AllLayers.Count();
        }
        //inserts Missing Layers
        if (sprite.AllLayers.Count() < layerCount)
            for (var i = sprite.AllLayers.Count(); i < layerCount; i++)
                _sprite.AddBlankLayer(uid, i);
        //Removes extra layers
        else if (sprite.AllLayers.Count() > layerCount)
            for (var i = sprite.AllLayers.Count() - 1; i >= layerCount; i--)
                _sprite.AddBlankLayer(uid, i);
        return true;
    }

    public bool TryHandleLayerConfiguration(Entity<SpriteComponent, PlayingCardStackComponent> uid, int cardCount, Func<Entity<SpriteComponent>, int, int, bool> layerFunc)
    {
        var sprite = uid.Comp1;
        var stack = uid.Comp2;

        // int = index of what card it is from
        List<(int, ISpriteLayer)> layers = [];

        var i = 0;
        foreach (var card in stack.Cards.TakeLast(cardCount))
        {
            if (!TryComp(card, out SpriteComponent? cardSprite))
                return false;
            layers.AddRange(cardSprite.AllLayers.Select(layer => (i, layer)));
            i++;
        }

        var j = 0;
        foreach (var obj in layers)
        {
            var (cardIndex, layer) = obj;
            _sprite.LayerSetVisible((uid, sprite) ,j, true);
            _sprite.LayerSetTexture((uid, sprite) ,j, layer.Texture);
            _sprite.LayerSetRsiState((uid, sprite) ,j, layer.RsiState.Name);
            layerFunc.Invoke((uid, sprite), cardIndex, j);
            j++;
        }

        return true;
    }
}