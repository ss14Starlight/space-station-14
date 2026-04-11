using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.EventSelector;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.EventSelector;

public sealed class EventSelectorRadialMenuBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private SimpleRadialMenu? _triggerRadialMenu;

    protected override void Open()
    {
        base.Open();

        _triggerRadialMenu = this.CreateWindow<SimpleRadialMenu>();

        if (!EntMan.TryGetComponent<EventSelectorRadialMenuComponent>(Owner, out var comp))
            return;

        var buttons = ConvertToButtons(comp.RadialMenuEntries);

        _triggerRadialMenu.SetButtons(buttons);
    }

    private List<RadialMenuOptionBase> ConvertToButtons(List<EventSelectorRadialMenuEntry> entries)
    {
        var buttons = new List<RadialMenuOptionBase>();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            var option = new RadialMenuActionOption<int>(TrySendTriggerSelectMessage, i)
            {
                ToolTip = entry.Name != null ? Loc.GetString(entry.Name) : null,

                IconSpecifier = entry.ProtoIdIcon != null
                                && _prototypeManager.Resolve(entry.ProtoIdIcon, out var iconProto)
                    ? RadialMenuIconSpecifier.With(iconProto)
                    : RadialMenuIconSpecifier.With(entry.SpriteSpecifierIcon),
            };

            buttons.Add(option);
        }

        return buttons;
    }

    private void TrySendTriggerSelectMessage(int index)
    {
        SendMessage(new EventSelectorOnRadialMenuSelectMessage(index));
    }
}
