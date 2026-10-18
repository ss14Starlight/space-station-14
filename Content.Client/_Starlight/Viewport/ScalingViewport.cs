// ReSharper disable once CheckNamespace
using System;
using System.Numerics;
using Robust.Client.UserInterface;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    public event Action<Vector2>? OnViewportMouseMove;

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        OnViewportMouseMove?.Invoke(args.GlobalPixelPosition.Position);
    }
    [Obsolete]
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            InvalidateViewport();

        base.Dispose(disposing);
    }
}
