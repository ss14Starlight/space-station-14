using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._Starlight.UserInterface.Controls;

/// <summary>
/// A thin vertical strip that reports horizontal drags, for resizing a neighbouring column.
/// </summary>
public sealed class ResizeHandle : Control
{
    /// <summary>
    /// Raised while dragging with the horizontal distance from where the drag started.
    /// </summary>
    public event Action<float>? OnDragged;

    public event Action? OnDragStarted;
    public event Action? OnDragFinished;

    private bool _dragging;
    private bool _hovered;
    private float _startX;

    public ResizeHandle()
    {
        MouseFilter = MouseFilterMode.Stop;
        DefaultCursorShape = CursorShape.HResize;
        MinWidth = 8;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragging = true;
        _startX = GlobalPosition.X + args.RelativePosition.X;
        OnDragStarted?.Invoke();
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick || !_dragging)
            return;

        _dragging = false;
        OnDragFinished?.Invoke();
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        _hovered = true;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hovered = false;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        // A faint grip line, brighter while it can be grabbed.
        var alpha = _dragging || _hovered ? 0.5f : 0.15f;
        var center = PixelSize.X / 2f;
        handle.DrawRect(new UIBox2(center - 1, 0, center + 1, PixelSize.Y), Color.White.WithAlpha(alpha));
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (_dragging)
            OnDragged?.Invoke(args.GlobalPosition.X - _startX);
    }
}
