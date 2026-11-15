using System;

namespace Content.Client._DEN.QuickConstruction.UI;

public sealed partial class QuickConstructionBoundUserInterface
{
    public event Action? RemoteMenuClosed;

    private void OnRemoteMenuClosed()
        => RemoteMenuClosed?.Invoke();
}
