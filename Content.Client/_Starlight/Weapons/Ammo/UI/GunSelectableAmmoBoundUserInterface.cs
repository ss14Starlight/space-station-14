using Content.Client.Stylesheets.Palette;
using Content.Client.UserInterface.Controls;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared._Starlight.Mech.Equipment.EntitySystems;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;
using Content.Shared._Starlight.Weapons.Ranged.Ammo;
using System.Linq;

namespace Content.Client._Starlight.Weapons.Ammo.UI;

public sealed partial class GunSelectableAmmoBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SimpleRadialMenu? _menu;
    private static readonly Color _selectedOptionBackground = Palettes.Green.Element.WithAlpha(128);
    private static readonly Color _selectedOptionHoverBackground = Palettes.Green.HoveredElement.WithAlpha(128);

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

        if (!EntMan.TryGetComponent<GunSelectableAmmoComponent>(Owner, out var selection))
            return;

        var settings = ConvertToButtons(selection.Settings, selection.Setting);

        _menu.SetButtons(settings);

    }

    private IEnumerable<RadialMenuOptionBase> ConvertToButtons(
        List<GunSelectableAmmoSetting> ammoTypes,
        int selectedAmmo)
    {
        var buttons = new List<RadialMenuOptionBase>();

        foreach (var (setting, i) in ammoTypes.Select((setting, i) => (setting, i)))
        {
            var option = new RadialMenuActionOption<int>(SendAmmoSelect, i)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(setting.Icon),
                ToolTip = Loc.GetString(setting.Name),
                BackgroundColor = (i == selectedAmmo) ? _selectedOptionBackground : null,
                HoverBackgroundColor = (i == selectedAmmo) ? _selectedOptionHoverBackground : null
            };
            buttons.Add(option);
        }

        return buttons;
    }

    private void SendAmmoSelect(int index)
        => SendPredictedMessage(new GunSelectableAmmoSelectMessage(index));
}
