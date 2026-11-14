using Content.Shared.Eui;
using Robust.Client.UserInterface.CustomControls;

// ReSharper disable CheckNamespace
namespace Content.Client.Administration.UI.Notes;

partial class AdminNotesEui
{
    private readonly HashSet<BaseWindow> _openWindows = [];

    private void OnSubWindowOpened(BaseWindow window)
    {
        _openWindows.Add(window);
        window.OnClose += () => OnSubWindowClosed(window);
    }

    private void OnSubWindowClosed(BaseWindow window)
    {
        if (_openWindows.Remove(window) && _openWindows.Count == 0)
            SendMessage(new CloseEuiMessage());
    }
}
