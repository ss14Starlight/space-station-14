using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.UserInterface.Controls;

/// <summary>
/// An <see cref="OptionButton"/> that supports per-item background tinting,
/// including the collapsed button face when an item is selected.
/// Call <see cref="SetItemColor"/> immediately after <see cref="OptionButton.AddItem(string,int?)"/> to tint the last-added item.
/// </summary>
public sealed class ColoredOptionButton : OptionButton
{
    private const float HoverLighten = 0.2f;
    private const float PressedDarken = 0.15f;
    private const float DisabledDarken = 0.25f;

    private const float ScaleSaturation = 0.6f;
    private const float ScaleBrightness = 0.6f;

    private const float ClampBrightnessMin = 0.1f;
    private const float ClampBrightnessMax = 0.5f;

    /// <summary>
    /// The Button component belonging to the last item added.
    /// </summary>
    private Button? _lastButton;

    /// <summary>
    /// The color of the button face, reflecting the currently selected item.
    /// </summary>
    private Color? _faceColor;

    private readonly Dictionary<int, Color> _itemColors = new();

    public ColoredOptionButton() => OnItemSelected += args => SelectId(args.Id);

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        ModulateSelfOverride = _faceColor is { } color
            ? DrawMode switch
            {
                DrawModeEnum.Hover    => Lighten(color, HoverLighten),
                DrawModeEnum.Pressed  => Darken(color, PressedDarken),
                DrawModeEnum.Disabled => Darken(color, DisabledDarken),
                _                     => color
            }
            : null;
    }

    /// <summary>
    /// Our parent class provides *very* little in the way of hooks and customization. By using this handler we can
    /// capture the Button control that the last
    /// <see cref="OptionButton.AddItem(Robust.Client.Graphics.Texture,string,int?)"/> call created. Then using
    /// <see cref="SetItemColor"/> lets you customize the color of the last added item.
    /// </summary>
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
                ApplyFaceColor(id);
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
        _faceColor = null;
        ModulateSelfOverride = null;
    }

    /// <summary>
    /// Applies the color corresponding to the given item ID to the face.
    /// </summary>
    /// <param name="id"></param>
    private void ApplyFaceColor(int id)
    {
        _faceColor = _itemColors.TryGetValue(id, out var color) ? color : null;
        DrawModeChanged();
    }

    /// <summary>
    /// Basically a build-a-button. Takes a Button control and base color, computes derivative colors for states,
    /// and applies them to the button when necessary via color modulation. This is necessary because we cannot subclass
    /// the Button control used by the OptionButton.
    /// </summary>
    /// <param name="normal">The normal color</param>
    /// <param name="button">The Button control</param>
    private static void ApplyColor(Color normal, Button button)
    {
        var hover = Lighten(normal, HoverLighten);
        var pressed = Darken(normal, PressedDarken);
        var disabled = Darken(normal, DisabledDarken);

        button.ModulateSelfOverride = normal;

        var isHovered = false;
        var isPressed = false;

        button.OnMouseEntered += _ =>
        {
            isHovered = true;
            PsuedoDrawModeChanged();
        };
        button.OnMouseExited += _ =>
        {
            isHovered = false;
            PsuedoDrawModeChanged();
        };
        button.OnButtonDown += _ =>
        {
            isPressed = true;
            PsuedoDrawModeChanged();
        };
        button.OnButtonUp += _ =>
        {
            isPressed = false;
            PsuedoDrawModeChanged();
        };
        return;

        void PsuedoDrawModeChanged()
        {
            if (button.Disabled)
                button.ModulateSelfOverride = disabled;
            else if (isPressed)
                button.ModulateSelfOverride = pressed;
            else if (isHovered)
                button.ModulateSelfOverride = hover;
            else
                button.ModulateSelfOverride = normal;
        }
    }

    /// <summary>
    /// Converts a full, probably high-contrast color to a muted, darker version that fits the UI
    /// and doesn't make the white button text unreadable.
    /// </summary>
    /// <param name="color">The color</param>
    /// <returns>The UI-ready color</returns>
    private static Color MakeButtonColor(Color color)
    {
        var hsv = Color.ToHsv(color);
        hsv.Y *= ScaleSaturation;
        hsv.Z = Math.Clamp(hsv.Z * ScaleBrightness, ClampBrightnessMin, ClampBrightnessMax);
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
