using Content.Shared._Starlight.Sprite;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Sprite;

public sealed partial class AppliedSpriteLayerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AppliedSpriteLayerComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<AppliedSpriteLayerComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnState(Entity<AppliedSpriteLayerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.Sprite == null || !TryComp<SpriteComponent>(ent.Owner, out var spriteComp)) return;

        Entity<SpriteComponent?> spriteEnt = (ent.Owner, spriteComp);
        var index = _sprite.LayerMapReserve(spriteEnt, ent.Comp.Layer);
        _sprite.LayerSetSprite(spriteEnt, index, ent.Comp.Sprite);
        if (ent.Comp.Glowing) spriteComp.LayerSetShader(index, "unshaded");
        _sprite.LayerSetVisible(spriteEnt, index, true);
    }

    private void OnShutdown(Entity<AppliedSpriteLayerComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var spriteComp)) return;

        Entity<SpriteComponent?> spriteEnt = (ent.Owner, spriteComp);
        var index = _sprite.LayerMapReserve(spriteEnt, ent.Comp.Layer);
        _sprite.LayerSetVisible(spriteEnt, index, false);
    }
}
