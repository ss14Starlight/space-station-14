using Content.Shared._Starlight.Mech.Components;
using Content.Shared._Starlight.Mech.EntitySystems;
using Content.Shared.Mech;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Mech.EntitySystems;

public sealed class MechSirenSystem : SharedMechSirenSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechSirenComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }

    private void OnAppearanceChanged(EntityUid uid, MechSirenComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not {} sprite)
            return;

        if (!_sprite.TryGetLayer((uid, sprite), MechVisualLayers.Siren, out var sirenLayer, false))
            return;

        _sprite.LayerSetVisible(sirenLayer, comp.Toggled);
    }
}
