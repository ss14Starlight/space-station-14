using System.Linq;
using Content.Shared._Funkystation.Footprints;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Funkystation.Footprints;

public sealed partial class FootprintSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    [SubscribeLocalEvent]
    private void OnStartup(Entity<FootprintComponent> entity, ref ComponentStartup args)
    {
        UpdateVisuals(entity);
    }

    [SubscribeLocalEvent]
    private void OnComponentState(Entity<FootprintComponent> entity, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(entity);
    }

    private void UpdateVisuals(Entity<FootprintComponent> entity)
    {
        if (!TryComp<SpriteComponent>(entity, out var spriteComp))
            return;

        var sprite = new Entity<SpriteComponent>(entity, spriteComp);
        var spriteNullable = sprite.AsNullable();

        var printsAndLayers = entity.Comp.Prints.Select((print, index) => (
            print,
            layer: _sprite.TryGetLayer(spriteNullable, index, out var l, logMissing: false)
                ? l
                : _sprite.AddBlankLayer(sprite, index)
        ));
        foreach (var (print, layer) in printsAndLayers)
        {
            _sprite.LayerSetOffset(layer, print.Offset);
            _sprite.LayerSetRotation(layer, print.Rotation);
            _sprite.LayerSetColor(layer, print.Color);
            _sprite.LayerSetSprite(layer, new SpriteSpecifier.Rsi(entity.Comp.Sprites, print.State));
        }
    }
}
