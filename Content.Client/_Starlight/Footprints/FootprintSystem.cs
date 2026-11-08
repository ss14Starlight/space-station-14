using System.Numerics;
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

    private void OnStartup(Entity<FootprintComponent> entity, ref ComponentStartup args) => UpdateVisuals(entity);

    private void OnStateUpdated(Entity<FootprintComponent> entity, ref AfterAutoHandleStateEvent args) => UpdateVisuals(entity);

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

            // Alternating steps are opposite feet. Sprites are drawn as left feet, so the
            // right-hand print is the mirrored one. Set every time so a reused layer can't
            // keep a stale mirror.
            _sprite.LayerSetScale(layer, new Vector2(print.Mirrored ? -1f : 1f, 1f));
            _sprite.LayerSetColor(layer, Color.White.WithAlpha(print.Alpha));
            _sprite.LayerSetSprite(layer, GetSprite(print.State, entity.Comp));
        }

        entity.Comp.RenderedPrintCount = entity.Comp.Prints.Count;
    }

    // One arm per state keeps the sheet and the state name together, so adding a state
    // cannot leave either behind.
    private static SpriteSpecifier.Rsi GetSprite(FootprintVisualState state, FootprintComponent component) => state switch
    {
        FootprintVisualState.Foot => new(component.Sprites, "foot"),
        FootprintVisualState.Dragging1 => new(component.Sprites, "dragging-1"),
        FootprintVisualState.Dragging2 => new(component.Sprites, "dragging-2"),
        FootprintVisualState.Dragging3 => new(component.Sprites, "dragging-3"),
        FootprintVisualState.Dragging4 => new(component.Sprites, "dragging-4"),
        FootprintVisualState.Dragging5 => new(component.Sprites, "dragging-5"),
        FootprintVisualState.Paw => new(component.SpeciesSprites, "paw"),
        FootprintVisualState.Barefoot => new(component.SpeciesSprites, "barefoot"),
        FootprintVisualState.Claw => new(component.SpeciesSprites, "claw"),
        FootprintVisualState.Talon => new(component.SpeciesSprites, "talon"),
        FootprintVisualState.Moth => new(component.SpeciesSprites, "moth"),
        FootprintVisualState.Spider => new(component.SpeciesSprites, "spider"),
        FootprintVisualState.Diona => new(component.SpeciesSprites, "diona"),
        _ => new(component.Sprites, "foot"),
    };
}
