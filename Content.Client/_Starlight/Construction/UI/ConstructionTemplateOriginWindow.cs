using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Starlight.Construction.UI;

/// <summary>
/// Asks whether an imported template should be placed where it was saved, or by hand.
/// </summary>
public sealed class ConstructionTemplateOriginWindow : DefaultWindow
{
    public event Action<bool>? Chosen;

    /// <summary>
    /// Creates and opens the origin selection window.
    /// </summary>
    public ConstructionTemplateOriginWindow()
    {
        Title = Loc.GetString("construction-template-origin-title");
        SetSize = new Vector2(400, 130);

        var savedButton = new Button
        {
            Text = Loc.GetString("construction-template-origin-saved"),
            HorizontalExpand = true,
        };

        var manualButton = new Button
        {
            Text = Loc.GetString("construction-template-origin-manual"),
            HorizontalExpand = true,
        };

        savedButton.OnPressed += _ =>
        {
            Chosen?.Invoke(true);
            Close();
        };

        manualButton.OnPressed += _ =>
        {
            Chosen?.Invoke(false);
            Close();
        };

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Children = { savedButton, manualButton },
        };

        var prompt = new RichTextLabel { VerticalExpand = true };
        prompt.SetMessage(Loc.GetString("construction-template-origin-prompt"));

        Contents.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children = { prompt, buttons },
        });

        OpenCentered();
    }
}
