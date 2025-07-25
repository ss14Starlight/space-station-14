using Content.Client._Starlight.UI;
using Content.Client.Message;
using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.CentComm;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.CentComm.UI;

[UsedImplicitly]
public sealed class CCManagmentConsoleBUI(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private FancyWindow? _window;

    private ItemSlotsSystem? _itemSlots;

    protected override void Open()
    {
        base.Open();

        _itemSlots ??= EntMan.System<ItemSlotsSystem>();
        if (_window == null)
        {
            string? disk1slot = null, disk2slot = null;
            if (EntMan.TryGetComponent<CentCommManagementConsoleComponent>(Owner, out var console))
            {
                disk1slot = console.PrimaryKey;
                disk2slot = console.SecondaryKey;
            }
            disk1slot ??= "disk1";
            disk2slot ??= "disk2";

            _window = new FancyWindow { Title = Loc.GetString("cc-management-console"), Resizable = false };
            var grid = new GridContainer { Rows = 2, Columns = 2, Margin = new Thickness(13,13,13,11)};
            _window.AddChild(grid);
            var disk1 = new RichTextLabel { Text = Loc.GetString("cc-management-console-disk1", ("status", Loc.GetString(_itemSlots.GetItemOrNull(Owner,disk1slot) != null ? "cc-management-console-disk-in" : "cc-management-console-disk-out")) )};
            var disk2 = new RichTextLabel { Text = Loc.GetString("cc-management-console-disk2", ("status", Loc.GetString(_itemSlots.GetItemOrNull(Owner,disk2slot) != null ? "cc-management-console-disk-in" : "cc-management-console-disk-out"))) };
            grid.AddChildren([disk1, disk2]);
            var sendert = new Button { Text = Loc.GetString("cc-management-console-sendert") };
            var aioverride = new Button { Text = Loc.GetString("cc-management-console-aioverride") };
            grid.AddChildren([sendert, aioverride]);
        }
        _window.OpenCentered();
    }
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        //_window?.Populate((PaperBoundUserInterfaceState) state);
    }
}
