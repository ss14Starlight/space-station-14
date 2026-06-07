using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.UserInterface.Controls;

/// <summary>
/// An <see cref="OptionButton"/> that supports per-item background tinting,
/// including the collapsed button face when an item is selected.
/// Call <see cref="SetItemColor"/> immediately after <see cref="OptionButton.AddItem"/> to tint the last-added item.
/// </summary>
public partial class ColoredOptionButton : OptionButton
{
    // ButtonOverride is called synchronously during AddItem, so _lastButton is
    // always the button for the item that was just added when SetItemColor runs.
    private Button? _lastButton;
    private readonly Dictionary<int, Color> _itemColors = new();

    public ColoredOptionButton()
    {
        // Handle user-driven selection changes (clicking an item in the popup).
        OnItemSelected += args => UpdateCollapsedColor(args.Id);
    }

    public override void ButtonOverride(Button button)
    {
        base.ButtonOverride(button);
        _lastButton = button;
    }

    public void SetItemColor(Color? color)
    {
        if (_lastButton is null)
            return;

        var id = GetItemId(ItemCount - 1);

        if (color is not { } c)
        {
            _itemColors.Remove(id);
            // Clear collapsed tint if this item is currently selected.
            if (SelectedId == id)
                ModulateSelfOverride = null;
            return;
        }

        var processed = MakeButtonColor(c);
        _itemColors[id] = processed;
        ApplyColor(processed, _lastButton);

        // If this item is already selected, tint the collapsed face immediately.
        if (SelectedId == id)
            UpdateCollapsedColor(id);
    }

    // Hide non-virtual SelectId/TrySelectId/Clear so programmatic selection
    // also updates the collapsed tint.
    public new void SelectId(int id)
    {
        base.SelectId(id);
        UpdateCollapsedColor(id);
    }

    public new bool TrySelectId(int id)
    {
        if (!base.TrySelectId(id))
            return false;
        UpdateCollapsedColor(id);
        return true;
    }

    public new void Clear()
    {
        base.Clear();
        _itemColors.Clear();
        ModulateSelfOverride = null;
    }

    public void UpdateCollapsedColor(int id)
    {
        ModulateSelfOverride = _itemColors.TryGetValue(id, out var color) ? color : null;
    }

    private static void ApplyColor(Color normal, Button button)
    {
        var hover = Lighten(normal, 0.20f);
        var pressed = Darken(normal, 0.15f);

        button.ModulateSelfOverride = normal;

        button.OnMouseEntered += _ =>
        {
            if (!button.Disabled)
                button.ModulateSelfOverride = hover;
        };
        button.OnMouseExited += _ =>
        {
            if (!button.Disabled)
                button.ModulateSelfOverride = normal;
        };
        button.OnPressed += _ =>
        {
            if (!button.Disabled)
                button.ModulateSelfOverride = pressed;
        };
    }

    private static Color MakeButtonColor(Color color)
    {
        var hsv = Color.ToHsv(color);
        hsv.Y *= Math.Min(hsv.Y, .6f);
        hsv.Z *= Math.Min(hsv.Z, .5f);
        return Color.FromHsv(hsv);
    }

    private static Color Lighten(Color color, float amount)
    {
        var hsv = Color.ToHsv(color);
        hsv.Z = Math.Min(1f, hsv.Z * (1f + amount));
        return Color.FromHsv(hsv);
    }

    private static Color Darken(Color color, float amount)
    {
        var hsv = Color.ToHsv(color);
        hsv.Z *= 1f - amount;
        return Color.FromHsv(hsv);
    }
}
