using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.UserInterface;

public static class PopOutExtensions
{
    /// <summary>
    /// Style name for the pop out button. Its look is set in the stylesheet (see StyleBase).
    /// </summary>
    public const string PopOutButtonStyleClass = "windowPopOutButton";

    /// <summary>
    /// Builds a pop outable window, closes the menu when the window closes, and opens it where it
    /// was last (or centered the first time).
    /// </summary>
    public static T CreatePopOutableWindow<T>(this BoundUserInterface bui, IEntityManager entMan) where T : Control, IPopOutWindow, new()
    {
        var window = bui.CreateDisposableControl<T>();
        window.OnFinalClose += bui.Close;

        // Register it so the game remembers where the window was last placed.
        var uiSystem = entMan.System<UserInterfaceSystem>();
        uiSystem.RegisterControl(bui, window);

        if (uiSystem.TryGetPosition(bui.Owner, bui.UiKey, out var position))
        {
            window.Open(position);
        }
        else
        {
            window.OpenCentered();
        }

        return window;
    }
}
