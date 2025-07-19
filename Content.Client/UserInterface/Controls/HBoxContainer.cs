using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Controls;

/// <summary>
/// A convenience wrapper for a horizontally oriented BoxContainer.
/// </summary>
public sealed class HBoxContainer : BoxContainer
{
    public HBoxContainer()
    {
        Orientation = LayoutOrientation.Horizontal;
    }
}
