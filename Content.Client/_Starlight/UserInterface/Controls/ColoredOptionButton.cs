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
        OnItemSelected += args => ApplyFaceColor(args.Id);
    }

    /// <summary>
    /// Our parent class provides *very* little in the way of hooks and customization. By using this handler we can
    /// capture the Button control that the last
    /// <see cref="OptionButton.AddItem(Robust.Client.Graphics.Texture,string,int?)"/> call created. Then using
    /// <see cref="SetItemColor"/>
    /// </summary>
    /// <param name="button"></param>
    public override void ButtonOverride(Button button)
    {
        base.ButtonOverride(button);
        _lastButton = button;
    }

    /// <summary>
    /// Sets the color of the last added item.
    /// </summary>
    /// <param name="color">The color</param>
    public void SetItemColor(Color? color)
    {
        if (_lastButton is null)
            return;

        var id = GetItemId(ItemCount - 1);

        // Delete and unset color if it is null.
        if (color is not { } c)
        {
            _itemColors.Remove(id);
            if (SelectedId == id)
                ModulateSelfOverride = null;
            return;
        }

        // Calculate the resulting button color from the base color, store it, and apply it.
        var processed = MakeButtonColor(c);
        _itemColors[id] = processed;
        ApplyColor(processed, _lastButton);

        // If this item is already selected, tint the main button immediately.
        if (SelectedId == id)
            ApplyFaceColor(id);
    }

    /// <summary>
    /// Set the selected item by index. Updates the main selector color to match the selected item.
    /// </summary>
    /// <param name="idx">The item index</param>
    public new void Select(int idx)
    {
        base.Select(idx);
        ApplyFaceColor(GetItemId(idx));
    }

    /// <summary>
    /// Set the selected item by ID (not index). Updates the face color to match the selected item.
    /// </summary>
    /// <param name="id">The item ID</param>
    public new void SelectId(int id)
    {
        base.SelectId(id);
        ApplyFaceColor(id);
    }

    /// <summary>
    /// Wipe all items and state.
    /// </summary>
    public new void Clear()
    {
        base.Clear();
        _itemColors.Clear();
        ModulateSelfOverride = null;
    }

    /// <summary>
    /// Applies the color corresponding to the given item ID to the face.
    /// </summary>
    /// <param name="id"></param>
    private void ApplyFaceColor(int id) =>
        ModulateSelfOverride = _itemColors.TryGetValue(id, out var color) ? color : null;

    /// <summary>
    /// Basically a build-a-button. Takes a Button control and base color, computes derivative colors for hover and pressed states,
    /// and applies them to the button.
    /// </summary>
    /// <param name="normal">The normal color</param>
    /// <param name="button">The Button control</param>
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

    /// <summary>
    /// Converts a full, probably high-contrast color to a muted, darker version that fits the UI
    /// and doesn't make the white text unreadable.
    /// </summary>
    /// <param name="color">The color</param>
    /// <returns>The UI-ready color</returns>
    private static Color MakeButtonColor(Color color)
    {
        var hsv = Color.ToHsv(color);
        hsv.Y *= Math.Min(hsv.Y, .6f); // Limit saturation to 60%.
        hsv.Z *= Math.Min(hsv.Z, .5f); // Limit brightness to 50%.
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
