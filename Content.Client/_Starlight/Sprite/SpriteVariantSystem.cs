using Content.Shared._Starlight.Sprite;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Sprite;

public sealed partial class SpriteVariantSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpriteVariantComponent, ComponentStartup>(OnVariantStartup);
        SubscribeLocalEvent<SpriteVariantComponent, AfterAutoHandleStateEvent>(OnVariantState);
    }

    private void OnVariantStartup(EntityUid uid, SpriteVariantComponent comp, ComponentStartup ev)
    {
        TryApplyVariant(uid, comp);
    }

    private void OnVariantState(EntityUid uid, SpriteVariantComponent comp, ref AfterAutoHandleStateEvent ev)
    {
        TryApplyVariant(uid, comp);
    }

    private void TryApplyVariant(EntityUid uid, SpriteVariantComponent variant)
    {
        if (string.IsNullOrEmpty(variant.Variant))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.LayerSetRsiState((uid, sprite), SpriteVariantLayers.Base, variant.Variant);
    }
}

public enum SpriteVariantLayers : byte
{
    Base,
}
