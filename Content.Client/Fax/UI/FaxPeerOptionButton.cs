using Content.Shared.Fax;
using Content.Shared.Gravity;
using Microsoft.Win32.SafeHandles;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Fax.UI;

public sealed class FaxPeerOptionButton : OptionButton
{
    private const float FaxColorBlendAmount = 0.20f;

    private delegate void PeerAdded(int index, KnownFax knownFax, Button button);

    private Button? _lastButton;

    public void AddFaxPeer(KnownFax knownFax)
    {
        AddItem(knownFax.Name);
        SetItemMetadata(ItemCount - 1, knownFax);
        ApplyFaxPeerColor(ItemCount - 1, knownFax, _lastButton!);
    }

    public override void ButtonOverride(Button button)
    {
        base.ButtonOverride(button);
        _lastButton = button;
    }

    private void ApplyFaxPeerColor(int index, KnownFax knownFax, Button button)
    {
        if (knownFax.GroupColor is not { } color)
            return;

        var normal = MakeFaxButtonColor(color);
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

    private StyleBox GetActualButtonStyleBox(Button button)
    {
        // button.Restyle();
        if (button.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var styleBox))
            return styleBox;

        return UserInterfaceManager.ThemeDefaults.ButtonStyle;
    }


    private static StyleBoxFlat BlendStyleBox(StyleBoxFlat styleBox, Color color, float amount)
    {


        var copy = new StyleBoxFlat(styleBox)
        {
            BackgroundColor = Color.Transparent
        };

        return copy;
    }

    private static Color MakeFaxButtonColor(Color color)
    {
        var hsv = Color.ToHsv(color);
        hsv.Y *= Math.Min(hsv.Y, .6f); // Limit saturation to 60%
        hsv.Z *= Math.Max(Math.Min(hsv.Z, .5f), .4f); // Limit brightness to 40-50%
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
