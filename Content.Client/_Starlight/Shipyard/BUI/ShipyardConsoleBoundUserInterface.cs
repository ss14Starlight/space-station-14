using Content.Client._Starlight.Shipyard.UI;
using Content.Shared._Starlight.Shipyard.Events;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client._Starlight.Shipyard.BUI;

public sealed class ShipyardConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ShipyardConsoleMenu? _menu;

    [ViewVariables]
    public int Balance { get; private set; }

    public ShipyardConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = new ShipyardConsoleMenu(this);
        _menu.OpenCentered();
        _menu.OnClose += Close;
        _menu.OnOrderApproved += ApproveOrder;

        _menu.PopulateCategories();
        _menu.PopulateProducts();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        if (_menu != null)
        {
            _menu.OnClose -= Close;
            _menu.OnOrderApproved -= ApproveOrder;

            _menu.Close();
            _menu = null;
        }
    }

    private void ApproveOrder(ButtonEventArgs args)
    {
        if (args.Button.Parent?.Parent is not VesselRow row || row.Vessel == null)
        {
            return;
        }

        var vesselId = row.Vessel.ID;
        SendMessage(new ShipyardConsolePurchaseMessage(vesselId));
    }
}
