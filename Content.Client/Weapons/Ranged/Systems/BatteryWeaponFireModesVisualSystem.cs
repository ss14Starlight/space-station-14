using Content.Shared.Weapons.Ranged.Components;
using Content.Client.Weapons.Ranged.Components;
using Robust.Client.GameObjects;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class BatteryWeaponFireModesVisualSystem : EntitySystem
{
    [Dependency] private GunSystem _gun = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BatteryWeaponFireModesComponent, AfterAutoHandleStateEvent>(OnFireModeChanged);
    }

    private void OnFireModeChanged(EntityUid uid, BatteryWeaponFireModesComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var fireMode = component.FireModes[component.CurrentFireMode];

        if (fireMode.VisualState != null
            && _sprite.LayerMapTryGet((uid, sprite), FireModesLayers.FireMode, out _, false))
            _sprite.LayerSetRsiState((uid, sprite), FireModesLayers.FireMode, fireMode.VisualState);

        if (TryComp<MagazineVisualsComponent>(uid, out var magVisualsComp) && fireMode.MagState != null)
            _gun.SetMagState(uid, fireMode.MagState, false, magVisualsComp);
    }
}
