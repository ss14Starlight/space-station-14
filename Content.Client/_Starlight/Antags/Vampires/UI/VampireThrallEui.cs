using Content.Client.Eui;

namespace Content.Client._Starlight.Antags.Vampires.UI;

public sealed class VampireThrallEui : BaseEui
{
    private readonly VampireThrallMenu _menu;

    public VampireThrallEui()
        => _menu = new VampireThrallMenu();

    public override void Opened()
        =>_menu.OpenCentered();

    public override void Closed()
    {
        base.Closed();

        _menu.Close();
    }
}
