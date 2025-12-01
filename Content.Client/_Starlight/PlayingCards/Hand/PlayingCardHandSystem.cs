using System.Linq;
using Content.Shared._Starlight.PlayingCards.Card;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.PlayingCards.Hand;

public sealed class PlayingCardHandSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _spriteSystem = default!;
    
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PlayingCardComponent, ComponentStartup>(OnComponentStartupEvent);
        SubscribeNetworkEvent<PlayingCardFlipUpdatedEvent>(OnFlip);
    }

    private void OnComponentStartupEvent(EntityUid uid, PlayingCardComponent comp, ComponentStartup args)
    {
        if (!TryComp(uid, out SpriteComponent? spriteComponent))
            return;

        for (var i = 0; i < spriteComponent.AllLayers.Count(); i++)
        {
            Log.Debug($"Layer {i}");
            if (!spriteComponent.TryGetLayer(i, out var layer) || layer.State.Name == null)
                continue;

            var rsi = layer.RSI ?? spriteComponent.BaseRSI;
            if (rsi == null)
                continue;

            Log.Debug("FOI");
            comp.FrontSpriteLayers?.Add(new SpriteSpecifier.Rsi(rsi.Path, layer.State.Name));
        }

        comp.BackSpriteLayers ??= comp.FrontSpriteLayers;
        Dirty(uid, comp);
        UpdateSprite(uid, comp);
    }

    private void OnFlip(PlayingCardFlipUpdatedEvent args)
    {
        if (!TryComp(GetEntity(args.Card), out PlayingCardComponent? comp))
            return;
        UpdateSprite(GetEntity(args.Card), comp);
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