// ReSharper disable CheckNamespace
// The namespace must match ActionButton for this partial class.
namespace Content.Client.UserInterface.Systems.Actions.Controls;

public sealed partial class ActionButton
{
    public bool RemoteSelected { get; private set; }

    public void SetRemoteSelected(bool selected)
    {
        RemoteSelected = selected;
        DrawModeChanged();
    }
}
