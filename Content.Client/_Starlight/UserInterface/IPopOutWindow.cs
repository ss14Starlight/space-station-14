using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.UserInterface;

/// <summary>
/// Lets both pop out window types (the plain one and the fancy one) be treated the same.
/// </summary>
public interface IPopOutWindow
{
    /// <summary>Fires when the window is closed for good (in-game or as a desktop window).</summary>
    event Action? OnFinalClose;

    /// <summary>Closes the desktop window if it's open.</summary>
    void DisposePopOut();

    // These already exist on BaseWindow; listed here so the helpers can open the window.
    void Open(Vector2 position);
    void OpenCentered();
}

/// <summary>
/// Shared code for popping a window out.
/// </summary>
internal static class PopOutHelper
{
    /// <summary>
    /// Moves <paramref name="content"/> into a real desktop window and shows it. The same controls
    /// are moved over so nothing loses its state.
    /// </summary>
    public static OSWindow Show(string title, Vector2 size, Control content, Action onClosed)
    {
        // Use the plain size, not pixel size: the engine scales it for us, so passing pixels would
        // make the window too big on zoomed-in monitors.
        var window = new OSWindow
        {
            Title = title,
            SetSize = size,
        };

        window.Closed += onClosed;

        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(Color.FromHex("#25252A")),
        };
        panel.AddChild(content);

        window.AddChild(panel);
        window.Show();

        return window;
    }
}
