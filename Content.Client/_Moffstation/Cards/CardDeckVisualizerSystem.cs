using System.Linq;
using System.Numerics;
using Content.Client._Moffstation.GameObjects;
using Content.Shared._Moffstation.Cards.Components;
using Content.Shared._Moffstation.Cards.Systems;
using Content.Shared._Moffstation.Extensions;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._Moffstation.Cards;

public sealed partial class CardDeckVisualizerSystem : ManagedLayerVisualizerSystem<PlayingCardDeckComponent>
{
    [Dependency] private readonly SharedPlayingCardsSystem _playingCards = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    protected override ref HashSet<string> GetSpriteLayersAdded(PlayingCardDeckComponent component) =>
        ref component.SpriteLayersAdded;

    protected override void AddLayersOnAppearanceChange(
        PlayingCardDeckComponent component,
        Entity<SpriteComponent?> sprite,
        AppearanceComponent appearance,
        LayerFactory layerFactory
    )
    {
        if (!_gameTiming.IsFirstTimePredicted)
            return;

        if (!AppearanceSystem.TryGetData<List<PlayingCardInDeck>>(
                sprite,
                PlayingCardStackVisuals.Cards,
                out var visibleCards,
                appearance
            ))
            return;

        var rotation = Angle.FromDegrees(90);
        var scale = new Vector2(component.Scale);
        foreach (var (cardIndex, cardInDeck) in visibleCards.Index())
        {
            if (_playingCards.GetComponent(cardInDeck)?.Sprite() is not { } currentLayers)
                continue;

            var offset = new Vector2(0, component.YOffset * cardIndex);
            foreach (var (currLayerIndex, currLayerData) in currentLayers.Index())
            {
                layerFactory(
                    $"{cardIndex}-{currLayerIndex}",
                    currLayerData.Plus(scale, rotation, offset)
                );
            }
        }
    }
}
