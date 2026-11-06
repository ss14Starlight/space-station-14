using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Controls;

public partial class FancyWindow
{
    private const float MinimumVisibleWindowEdge = 30f;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!IsMeasureValid || Parent is not LayoutContainer parent || Root is not { } root)
            return;

        var boundsMin = Vector2.Zero;
        var boundsMax = root.Size;
        // Embedded windows can be clipped before reaching the edge of the screen.
        for (var ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
        {
            if (!ancestor.RectClipContent)
                continue;

            boundsMin = Vector2.Max(boundsMin, ancestor.GlobalPosition);
            boundsMax = Vector2.Min(boundsMax, ancestor.GlobalPosition + ancestor.Size);
        }

        var available = boundsMax - boundsMin;
        if (available.X <= 0 || available.Y <= 0 || Size.X <= 0 || Size.Y <= 0)
            return;

        var visible = Vector2.Min(new Vector2(MinimumVisibleWindowEdge), Vector2.Min(Size, available));
        var parentPosition = parent.GlobalPosition;
        var min = new Vector2(boundsMin.X + visible.X - Size.X, boundsMin.Y) - parentPosition;
        var max = boundsMax - visible - parentPosition;
        var position = Vector2.Clamp(Position, min, max);
        if (Position != position)
            LayoutContainer.SetPosition(this, position);
    }
}
