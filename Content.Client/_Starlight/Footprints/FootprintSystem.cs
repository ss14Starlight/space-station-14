using Content.Shared._Funkystation.Footprints;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Footprints;

// Starlight, had to update this quite a bit to use EntityQuery.
public sealed partial class FootprintSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FootprintComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FootprintComponent, AfterAutoHandleStateEvent>(OnStateUpdated);
    }

    private void OnStartup(Entity<FootprintComponent> entity, ref ComponentStartup args)
    {
        UpdateVisuals(entity);
    }

    private void OnStateUpdated(Entity<FootprintComponent> entity, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(entity);
    }

    private void UpdateVisuals(Entity<FootprintComponent> entity)
    {
        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return;

        var spriteEntity = new Entity<SpriteComponent>(entity, sprite);
        var nullableSprite = spriteEntity.AsNullable();

        // The reagent mixture recolors the whole tile. Keeping that tint on the sprite lets state updates append
        // only the new print instead of reconfiguring every existing layer.
        _sprite.SetColor(nullableSprite, entity.Comp.BaseColor);

        for (var i = entity.Comp.RenderedPrintCount; i < entity.Comp.Prints.Count; i++)
        {
            var print = entity.Comp.Prints[i];
            var layer = _sprite.TryGetLayer(nullableSprite, i, out var existing, logMissing: false)
                ? existing
                : _sprite.AddBlankLayer(spriteEntity, i);

            _sprite.LayerSetOffset(layer, print.Offset);
            _sprite.LayerSetRotation(layer, print.Rotation);
            _sprite.LayerSetColor(layer, Color.White.WithAlpha(print.Alpha));
            _sprite.LayerSetSprite(layer, new SpriteSpecifier.Rsi(entity.Comp.Sprites, GetState(print.State)));
        }

        entity.Comp.RenderedPrintCount = entity.Comp.Prints.Count;
    }

    private static string GetState(FootprintVisualState state)
    {
        return state switch
        {
            FootprintVisualState.Foot => "foot",
            FootprintVisualState.Dragging1 => "dragging-1",
            FootprintVisualState.Dragging2 => "dragging-2",
            FootprintVisualState.Dragging3 => "dragging-3",
            FootprintVisualState.Dragging4 => "dragging-4",
            FootprintVisualState.Dragging5 => "dragging-5",
            _ => "foot",
        };
    }
}
