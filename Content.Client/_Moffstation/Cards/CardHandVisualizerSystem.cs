using System.Linq;
using System.Numerics;
using Content.Client._Moffstation.GameObjects;
using Content.Shared._Moffstation.Cards.Components;
using Content.Shared._Moffstation.Cards.Systems;
using Content.Shared._Moffstation.Extensions;
using Content.Shared._Moffstation.Strip.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Moffstation.Cards;

public sealed partial class CardHandVisualizerSystem : ManagedLayerVisualizerSystem<PlayingCardHandComponent>
{
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedPlayingCardsSystem _playingCards = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayingCardHandComponent, GetHideInStripMenuEntityEvent>(HandOnGetHideInStripMenuEntity);
    }

    protected override ref HashSet<string> GetSpriteLayersAdded(PlayingCardHandComponent component) =>
        ref component.SpriteLayersAdded;

    protected override void AddLayersOnAppearanceChange(
        PlayingCardHandComponent component,
        Entity<SpriteComponent?> sprite,
        AppearanceComponent appearance,
        LayerFactory layerFactory
    )
    {
        if (!AppearanceSystem.TryGetData<List<PlayingCardInDeck>>(
                sprite,
                PlayingCardStackVisuals.Cards,
                out var visibleCards,
                appearance
            ))
            return;

        // Use the hand's contained cards' CURRENT sprites. This means that if the cards are face up / down, they will
        // appear that way in the hand as well.
        ApplyContainedCardLayersToHandSprite(component, visibleCards, forceUseReverseSprites: false, layerFactory);
    }

    private void HandOnGetHideInStripMenuEntity(
        Entity<PlayingCardHandComponent> entity,
        ref GetHideInStripMenuEntityEvent args
    )
    {
        if (!AppearanceSystem.TryGetData<List<PlayingCardInDeck>>(entity,
                PlayingCardStackVisuals.Cards,
                out var visibleCards))
            return;

        var virtualHand = Spawn(null, args.SpawnAt);
        var meta = MetaData(entity);
        var virtualMeta = MetaData(virtualHand);
        _meta.SetEntityName(virtualHand, meta.EntityName, virtualMeta, raiseEvents: false);
        _meta.SetEntityDescription(virtualHand, meta.EntityDescription, virtualMeta);

        var virtHandSprite = new Entity<SpriteComponent?>(virtualHand, AddComp<SpriteComponent>(virtualHand));

        ApplyContainedCardLayersToHandSprite(
            entity.Comp,
            visibleCards,
            // Use the hand's contained cards' REVERSE sprites. This means that regardless of the cards' facing
            // directions, they will appear face down in the virtual hand.
            forceUseReverseSprites: true,
            (_, layerData) =>
            {
                var idx = SpriteSystem.AddLayer(virtHandSprite, layerData, index: null);
                // ReSharper disable once RedundantAssignment // It's used by a debug assert, you piece.
                var gotLayer = SpriteSystem.TryGetLayer(virtHandSprite, idx, out var layer, logMissing: true);
                DebugTools.Assert(gotLayer);
                return layer ?? SpriteSystem.AddBlankLayer((virtHandSprite, virtHandSprite.Comp!));
            }
        );

        args.Entity = virtualHand;
    }

    private void ApplyContainedCardLayersToHandSprite(
        PlayingCardHandComponent component,
        List<PlayingCardInDeck> visibleCards,
        bool forceUseReverseSprites,
        LayerFactory layerFactory
    )
    {
        bool? faceDownOverride = forceUseReverseSprites ? true : null;
        var startingAngle = -(component.Angle / 2);
        var intervalAngle = visibleCards.Count != 1 ? component.Angle / (visibleCards.Count - 1) : 0;
        var startingXOffset = -(component.XOffset / 2);
        var intervalOffset = visibleCards.Count != 1 ? component.XOffset / (visibleCards.Count - 1) : 0;
        var layerScale = new Vector2(component.Scale, component.Scale);
        foreach (var (cardIndex, cardInDeck) in visibleCards.Index())
        {
            if (_playingCards.GetComponent(cardInDeck)?.Sprite(faceDownOverride) is not { } currentLayers)
                continue;

            var rotation = -Angle.FromDegrees(startingAngle + cardIndex * intervalAngle);
            var x = startingXOffset + cardIndex * intervalOffset;
            var offset = new Vector2(x, -(x * x) + 0.10f);
            foreach (var (currLayerIndex, currLayerData) in currentLayers.Index())
            {
                layerFactory(
                    $"{cardIndex}-{currLayerIndex}",
                    currLayerData.Plus(layerScale, rotation, offset)
                );
            }
        }
    }
}
