using Robust.Client.UserInterface.Controls;

namespace Content.Client._Funkystation.VendingMachines.UI;

public sealed partial class VendingMachineGridSlot : PanelContainer
{
    public void SetPrice(int price, bool showPrices)
    {
        PriceLabel.Visible = showPrices && price > 0;
        PriceLabel.Text = $"{price} ₡";
    }
}
