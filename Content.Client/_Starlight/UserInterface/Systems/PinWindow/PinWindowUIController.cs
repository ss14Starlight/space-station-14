using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Starlight.UserInterface.Systems.PinWindow;

public sealed partial class PinWindowUIController : UIController
{
    [Dependency] private IUserInterfaceManager _ui = null!;

    public const string StyleClassPinButtonPinned = "pinButtonPinned";
    public const string StyleClassPinButtonUnpinned = "pinButtonUnpinned";

    private static readonly AttachedProperty<bool> _pinnedProperty =
        AttachedProperty<bool>.Create("Pinned", typeof(PinWindowUIController));

    private static readonly AttachedProperty<TextureButton?> _pinButtonProperty =
        AttachedProperty<TextureButton?>.CreateNull("PinButton", typeof(PinWindowUIController));

    private static readonly AttachedProperty<bool> _pinnableProperty =
        AttachedProperty<bool>.Create("Pinnable", typeof(PinWindowUIController), true);

    public override void Initialize() =>
        _ui.WindowRoot.OnChildAdded += OnWindowAdded;

    public static bool IsPinned(BaseWindow window) => window.GetValue(_pinnedProperty);

    /// <summary>
    /// Opts a window in or out of getting a pin button. Must be set before the window is first opened.
    /// </summary>
    public static void SetPinnable(BaseWindow window, bool pinnable) => window.SetValue(_pinnableProperty, pinnable);

    private static void OnWindowAdded(Control control)
    {
        if (control is not BaseWindow window || !window.GetValue(_pinnableProperty)) return;
        if (window.GetValue(_pinButtonProperty) is not null) return;
        if (FindCloseButton(window) is not { Parent: { } header } close) return;

        var pin = new TextureButton
        {
            StyleClasses = { StyleClassPinButtonUnpinned },
            VerticalAlignment = Control.VAlignment.Center
        };
        pin.OnPressed += _ => TogglePin(window);
        header.AddChild(pin);

        pin.SetPositionInParent(close.GetPositionInParent());
        window.SetValue(_pinButtonProperty, pin);
    }

    private static void TogglePin(BaseWindow window)
    {
        var button = window.GetValue(_pinButtonProperty);
        if (button is null) return;
        var pinned = !window.GetValue(_pinnedProperty);
        window.SetValue(_pinnedProperty, pinned);
        button.SetOnlyStyleClass(pinned
            ? StyleClassPinButtonPinned
            : StyleClassPinButtonUnpinned);
    }

    private static TextureButton? FindCloseButton(BaseWindow window)
    {
        // Preferred over foreach to ensure it grabs the outermost thing just in case
        var queue = new Queue<Control>(window.Children);
        while (queue.TryDequeue(out var control))
        {
            if (control is TextureButton button && button.HasStyleClass(DefaultWindow.StyleClassWindowCloseButton))
                return button;
            if (window is DefaultWindow defaultWindow && control == defaultWindow.Contents) continue;
            foreach (var child in control.Children)
                queue.Enqueue(child);
        }
        return null;
    }
}
