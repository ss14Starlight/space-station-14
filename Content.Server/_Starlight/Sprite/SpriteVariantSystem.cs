using Content.Shared._Starlight.Sprite;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Sprite;

public sealed partial class SpriteVariantSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpriteVariantComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, SpriteVariantComponent comp, MapInitEvent ev)
    {
        if (!string.IsNullOrEmpty(comp.Variant) || comp.AvailableVariants.Count == 0)
            return;

        comp.Variant = _random.Pick(comp.AvailableVariants);
        Dirty(uid, comp);
    }
}
