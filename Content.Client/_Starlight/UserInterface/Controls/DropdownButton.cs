using System.Numerics;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.UserInterface.Controls;

/// <summary>
/// A button that opens a popup with a panel of controls when pressed.
/// </summary>
public sealed class DropdownButton : Button
{
    public const string StyleClassDropdownPanel = "DropdownPanel";
    public const string StyleClassDropdownItem = "DropdownItem";

    private readonly Popup _popup = new();
    private readonly PanelContainer _panel = new();
    private readonly HashSet<BaseButton> _items = new();

    public DropdownButton()
    {
        _panel.AddStyleClass(StyleClassDropdownPanel);
        _popup.AddChild(_panel);
        OnPressed += _ => Open();
    }

    public void SetContent(Control content)
    {
        content.Orphan();
        _panel.RemoveAllChildren();
        _panel.AddChild(content);
        HookItems(content);
    }

    private void HookItems(Control control)
    {
        foreach (var child in control.Children)
        {
            if (child is BaseButton button)
                RegisterItem(button);
            else
                HookItems(child);
        }
    }

    /// <summary>
    /// Registers a button to be part of the dropdown. When the button is pressed, the dropdown will close unless the button is a CheckBox or a ConfirmButton that is currently confirming.
    /// </summary>
    public void RegisterItem(BaseButton button)
    {
        if (!_items.Add(button))
            return;

        if (button is Button)
            button.AddStyleClass(StyleClassDropdownItem);

        button.OnPressed += _ =>
        {
            if (button is not CheckBox and not ConfirmButton { IsConfirming: true })
                _popup.Close();
        };
    }

    private void Open()
    {
        if (Root is not { } root)
            return;

        if (_popup.Parent != root.ModalRoot)
        {
            _popup.Orphan();
            root.ModalRoot.AddChild(_popup);
        }

        _popup.Measure(Vector2Helpers.Infinity);
        var size = _popup.DesiredSize;

        var pos = GlobalPosition + new Vector2(0, Height);
        pos.X = Math.Min(pos.X, root.Width - size.X);
        _popup.Open(UIBox2.FromDimensions(pos, size));
    }
}
