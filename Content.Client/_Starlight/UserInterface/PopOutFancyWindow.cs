using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.UserInterface;

/// <summary>
/// A <see cref="FancyWindow"/> with a pop-out button in its header. Pressing it moves the window's
/// contents into a real desktop window so it can be dragged to another monitor. The controls are
/// moved over, not rebuilt, so nothing loses its state. Fancy version of <see cref="PopOutWindow"/>.
/// </summary>
public abstract class PopOutFancyWindow : FancyWindow, IPopOutWindow
{
    /// <summary>
    /// The content that moves out into the desktop window. Subclasses point this at their root container.
    /// </summary>
    protected abstract Control Control { get; }

    /// <summary>Fires when the window is closed for good (in-game or as a desktop window).</summary>
    public event Action? OnFinalClose;

    /// <summary>Fires right after the contents move into the desktop window.</summary>
    public event Action? OnPopout;

    private OSWindow? _popOutWindow;
    private Control? _contentParent; // Where the contents lived before being popped out, so they can be returned.

    protected PopOutFancyWindow()
    {
        OnClose += FinalClose;
        AddButton();
    }

    private void AddButton()
    {
        // Drop our button next to the close button so we don't care how the header is laid out.
        if (CloseButton.Parent is not { } header)
            return;

        var button = new TextureButton
        {
            StyleClasses = { PopOutExtensions.PopOutButtonStyleClass },
            VerticalAlignment = VAlignment.Center,
            ToolTip = "Pop Out",
            Margin = new Thickness(0, 0, 6, 0),
        };

        header.AddChild(button);
        button.SetPositionInParent(header.ChildCount - 2);
        button.OnPressed += _ =>
        {
            // We're about to close the in-game window on purpose, so don't treat that as closing for good.
            OnClose -= FinalClose;

            // Remember where the contents live so they can be returned when the desktop window closes.
            _contentParent = Control.Parent;
            Control.Orphan();
            Close();

            _popOutWindow = PopOutHelper.Show(Title ?? string.Empty, Size, Control, OnPopOutClosed);

            OnPopout?.Invoke();
        };
    }

    /// <summary>
    /// When we close a popout return the contents back to the in-game window where applicable.
    /// If the window is disposed when closed like BUI's we just dispose it all.
    /// </summary>
    private void OnPopOutClosed()
    {
        // desktop window closes async a frame after close(), so if the window is already gone
        // by the time we run this and try to reparent there is nothing to reparent and we crash.
        // This makes sure we have something to reparent.
        if (_contentParent is { Disposed: false } parent && !Control.Disposed)
        {
            Control.Orphan();
            parent.AddChild(Control);
        }

        _contentParent = null;
        _popOutWindow = null;
        OnFinalClose?.Invoke();
    }

    private void FinalClose() => OnFinalClose?.Invoke();

    public void DisposePopOut() => _popOutWindow?.Close();
}
