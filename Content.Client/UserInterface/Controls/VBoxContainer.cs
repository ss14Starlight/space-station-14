using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Controls;

/// <summary>
/// A convenience wrapper for a vertically oriented BoxContainer.
/// </summary>
public sealed class VBoxContainer : BoxContainer
{
    public VBoxContainer()
    {
        Orientation = LayoutOrientation.Vertical;
    }
}
