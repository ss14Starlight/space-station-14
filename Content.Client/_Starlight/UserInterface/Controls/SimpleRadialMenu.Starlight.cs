using Robust.Client.UserInterface;
using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Controls;

public sealed partial class SimpleRadialMenu
{
    public void OpenEmbedded(Control parent, Vector2 position)
    {
        Measure(Vector2Helpers.Infinity);
        Parent?.RemoveChild(this);
        parent.AddChild(this);
        LayoutContainer.SetPosition(this, position - DesiredSize / 2);
    }
}
