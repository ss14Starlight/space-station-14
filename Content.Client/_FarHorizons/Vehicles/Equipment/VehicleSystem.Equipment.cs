
using Content.Shared._FarHorizons.Vehicles.Components;
using Robust.Client.GameObjects;

namespace Content.Client._FarHorizons.Vehicles.Equipment;
public sealed partial class VehicleEquipmentSystems : EntitySystem
{    
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleEquipmentComponent, AppearanceChangeEvent>(OnAppearanceChange);
        base.Initialize();
    }
    
    private void OnAppearanceChange(EntityUid uid, VehicleEquipmentComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;
        
        if (_appearance.TryGetData(uid, EquipmentVisuals.Hidden, out bool hidden))
            _sprite.SetVisible((uid, args.Sprite), !hidden);
    }
}