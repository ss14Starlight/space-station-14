using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Input;
using Robust.Shared.Utility;
using Content.Client.Guidebook.RichText;
using Content.Client.Guidebook.Richtext;
using Robust.Client.Graphics;
using Content.Client._Starlight.UI;

namespace Content.Client._Starlight.UserInterface.RichText;

[UsedImplicitly]
public sealed partial class BackgroundColorTagHandler : IMarkupTagHandler
{
    public BackgroundColorTagHandler() { }

    public string Name => "bgcolor";

    /// <inheritdoc/>
    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        var box = new StyleBoxFlat();
        if (!node.Value.TryGetString(out var text))
        {
            control = null;
            return false;
        }

        if (node.Attributes.TryGetValue("color", out var colorParam)
            && colorParam.TryGetString(out var color))
            box.BackgroundColor = Color.FromHex(color);

        box.BorderThickness = node.Attributes.TryGetValue("othick", out var outlineThicknessParam)
            && outlineThicknessParam.TryGetString(out var outlineThickness)
            ? new Thickness(float.Parse(outlineThickness))
            : new Thickness(1);

        box.BorderColor = node.Attributes.TryGetValue("ocolor", out var outlineColorParam)
            && outlineColorParam.TryGetString(out var outlineColor)
            ? Color.FromHex(outlineColor)
            : Color.White;

        var label = new Label();
        label.Text = text;

        var pbox = new PanelContainer();
        pbox.PanelOverride = box;
        pbox.AddChild(label);

        control = pbox;
        return true;
    }
}
