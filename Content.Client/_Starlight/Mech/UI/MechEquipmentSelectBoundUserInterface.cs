using Content.Client.Stylesheets.Palette;
using Content.Client.UserInterface.Controls;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared._Starlight.Mech.Equipment.EntitySystems;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Mech.UI;

public sealed partial class MechEquipmentSelectBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SimpleRadialMenu? _menu;
    private static readonly Color _selectedOptionBackground = Palettes.Green.Element.WithAlpha(128);
    private static readonly Color _selectedOptionHoverBackground = Palettes.Green.HoveredElement.WithAlpha(128);

    private readonly SpriteSpecifier.Texture _noEquipIcon = new (new ResPath("/Textures/Interface/Default/blocked.png"));

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        Update();
        _menu.OpenOverMouseScreenPosition();
    }

    public override void Update()
    {
        if (_menu == null)
            return;

        if (!EntMan.TryGetComponent<MechComponent>(Owner, out var mech))
            return;

        var models = ConvertToButtons(mech.EquipmentContainer.ContainedEntities, mech.CurrentSelectedEquipment);

        _menu.SetButtons(models);

    }

    private IEnumerable<RadialMenuOptionBase> ConvertToButtons(
        IEnumerable<EntityUid> installedEquipment,
        EntityUid? currentEquipment
    )
    {

        var buttons = new List<RadialMenuOptionBase>();

        var noEquipOption = new RadialMenuActionOption<NetEntity?>( SendToolDeselect, null)
        {
            IconSpecifier = RadialMenuIconSpecifier.With(_noEquipIcon),
            ToolTip = Loc.GetString("mech-equipment-select-none-popup"),
            BackgroundColor = !currentEquipment.HasValue ? _selectedOptionBackground : null,
            HoverBackgroundColor = !currentEquipment.HasValue ? _selectedOptionHoverBackground : null
        };
        buttons.Add(noEquipOption);

        foreach (var equipment in installedEquipment)
        {
            if (!EntMan.TryGetComponent<MetaDataComponent>(equipment, out var metadata))
                continue;

            if (!EntMan.TryGetComponent<MechEquipmentComponent>(equipment, out var equipComp)
                || equipComp.EquipmentType != EquipmentType.Active)
                continue;

            var option = new RadialMenuActionOption<NetEntity>(SendToolSelect, EntMan.GetNetEntity(equipment))
            {
                IconSpecifier = RadialMenuIconSpecifier.With(equipment),
                ToolTip = metadata.EntityName,
                BackgroundColor = (equipment == currentEquipment) ? _selectedOptionBackground : null,
                HoverBackgroundColor = (equipment == currentEquipment) ? _selectedOptionHoverBackground : null
            };
            buttons.Add(option);
        }

        return buttons;
    }

    private void SendToolDeselect(NetEntity? equipmentId)
        => SendPredictedMessage(new MechActiveEquipmentSelectMessage(null));

    private void SendToolSelect(NetEntity equipmentId)
        => SendPredictedMessage(new MechActiveEquipmentSelectMessage(equipmentId));
}
