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

        for (var i = 0; i < entity.Comp.Prints.Count; i++)
        {
            var print = entity.Comp.Prints[i];
            var layer = _sprite.TryGetLayer(nullableSprite, i, out var existing, logMissing: false)
                ? existing
                : _sprite.AddBlankLayer(spriteEntity, i);

            _sprite.LayerSetOffset(layer, print.Offset);
            _sprite.LayerSetRotation(layer, print.Rotation);
            _sprite.LayerSetColor(layer, print.Color);
            _sprite.LayerSetSprite(layer, new SpriteSpecifier.Rsi(entity.Comp.Sprites, print.State));
        }
    }
}
