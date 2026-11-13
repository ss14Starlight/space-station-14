using Content.Client.Eui;

namespace Content.Client._Starlight.CosmicCult.UI;

public sealed class DeconvertedCosmicEui : BaseEui
{
    private readonly DeconvertedCosmicMenu _menu;

    public DeconvertedCosmicEui()
        => _menu = new DeconvertedCosmicMenu();

    public override void Opened()
        => _menu.OpenCentered();

    public override void Closed()
    {
        base.Closed();

        _menu.Close();
    }
}
