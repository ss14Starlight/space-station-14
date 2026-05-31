using Content.Shared.Popups;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;

namespace Content.Shared._Starlight.Mech.Equipment.EntitySystems;

public sealed class SharedMechEquipmentSelectSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MechComponent, MechToggleEquipmentEvent>(OnSelectEquipmentAction);
        SubscribeLocalEvent<MechComponent, MechActiveEquipmentSelectMessage>(OnRadialSelected);
    }

    private void OnSelectEquipmentAction(EntityUid uid, MechComponent comp, MechToggleEquipmentEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<UserInterfaceComponent>(uid, out var uiComp))
            return;

        args.Handled = true;

        if (!_ui.IsUiOpen((uid, uiComp), MechEquipmentSelectUiKey.Key, args.Performer))
            _ui.OpenUi((uid, uiComp), MechEquipmentSelectUiKey.Key, args.Performer);
    }

    private void OnRadialSelected(EntityUid uid, MechComponent comp, MechActiveEquipmentSelectMessage msg)
    {
        var equipment = GetEntity(msg.SelectedEquipment);

        if (equipment.HasValue)
        {
            if (!TryComp<MechEquipmentComponent>(equipment.Value, out var equipComp)
                || equipComp.EquipmentType != EquipmentType.Active
                || !comp.EquipmentContainer.Contains(equipment.Value))
                return;
        }

        comp.CurrentSelectedEquipment = equipment;

        var popupString = comp.CurrentSelectedEquipment != null
            ? Loc.GetString("mech-equipment-select-popup", ("item", comp.CurrentSelectedEquipment))
            : Loc.GetString("mech-equipment-select-none-popup");

        _popup.PopupPredicted(popupString, uid, comp.PilotSlot.ContainedEntity);

        Dirty(uid, comp);
    }

}
