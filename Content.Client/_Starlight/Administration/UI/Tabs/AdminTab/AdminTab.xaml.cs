using Content.Client._Starlight.Administration.UI.Tabs.AdminTab;
using Robust.Client.UserInterface.XAML;

// ReSharper disable CheckNamespace
namespace Content.Client.Administration.UI.Tabs.AdminTab;

public sealed partial class AdminTab
{
    public AdminTab()
    {
        RobustXamlLoader.Load(this);
        var shuttleWindow = new SLAdminShuttleWindow();
        CallShuttleButton.OnPressed += _ => shuttleWindow.OpenCentered();
    }
}
