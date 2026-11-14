using System.Numerics;
using Content.Shared._Funkystation.Footprints;
using Content.Shared._Starlight.CCVar;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Footprints;

// Starlight, had to update this quite a bit to use EntityQuery.
public sealed partial class FootprintSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private FootprintVisibilityMode _visibility = FootprintVisibilityMode.All;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FootprintComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FootprintComponent, AfterAutoHandleStateEvent>(OnStateUpdated);
        Subs.CVar(_cfg, StarlightCCVars.FootprintVisibility, OnVisibilityChanged, true);
    }

    private void OnVisibilityChanged(int value)
    {
        var visibility = value switch
        {
            (int) FootprintVisibilityMode.Half => FootprintVisibilityMode.Half,
            (int) FootprintVisibilityMode.None => FootprintVisibilityMode.None,
            _ => FootprintVisibilityMode.All,
        };

        if (_visibility == visibility)
            return;

        _visibility = visibility;

        var query = EntityQueryEnumerator<FootprintComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var footprint, out var sprite))
        {
            ClearVisuals((uid, footprint), sprite);
            UpdateVisuals((uid, footprint));
        }
    }

    private void ClearVisuals(Entity<FootprintComponent> entity, SpriteComponent sprite)
    {
        for (var i = entity.Comp.RenderedPrintCount - 1; i >= 0; i--)
            _sprite.RemoveLayer((entity.Owner, sprite), i);

        entity.Comp.RenderedPrintCount = 0;
        entity.Comp.ProcessedPrintCount = 0;
    }

    private void OnStartup(Entity<FootprintComponent> entity, ref ComponentStartup args) => UpdateVisuals(entity);

    private void OnStateUpdated(Entity<FootprintComponent> entity, ref AfterAutoHandleStateEvent args) => UpdateVisuals(entity);

    private void UpdateVisuals(Entity<FootprintComponent> entity)
    {
        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return;

        var spriteEntity = new Entity<SpriteComponent>(entity, sprite);
        var nullableSprite = spriteEntity.AsNullable();

        if (_visibility == FootprintVisibilityMode.None)
        {
            _sprite.SetVisible(nullableSprite, false);
            return;
        }

        if (entity.Comp.ProcessedPrintCount > entity.Comp.Prints.Count)
            ClearVisuals(entity, sprite);

        // The reagent mixture recolors the whole tile. Keeping that tint on the sprite lets state updates append
        // only the new print instead of reconfiguring every existing layer.
        _sprite.SetColor(nullableSprite, entity.Comp.BaseColor);

        var half = _visibility == FootprintVisibilityMode.Half;

        // Choose which alternating prints to show on each tile, for consistency so it doesn't change every time you look away
        var consistency = half ? HashCode.Combine(GetNetEntity(entity.Owner)) & 1 : 0;

        for (var i = entity.Comp.ProcessedPrintCount; i < entity.Comp.Prints.Count; i++)
        {
            if (half && (i & 1) != consistency)
                continue;

            var print = entity.Comp.Prints[i];
            var layer = _sprite.AddBlankLayer(spriteEntity);

            _sprite.LayerSetOffset(layer, print.Offset);
            _sprite.LayerSetRotation(layer, print.Rotation);

            // Alternating steps are opposite feet. Sprites are drawn as left feet, so mirror right-hand prints.
            _sprite.LayerSetScale(layer, new Vector2(print.Mirrored ? -1f : 1f, 1f));
            _sprite.LayerSetColor(layer, Color.White.WithAlpha(print.Alpha));
            _sprite.LayerSetSprite(layer, GetSprite(print.State, entity.Comp));
            entity.Comp.RenderedPrintCount++;
        }

        entity.Comp.ProcessedPrintCount = entity.Comp.Prints.Count;
        _sprite.SetVisible(nullableSprite, entity.Comp.RenderedPrintCount > 0);
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
