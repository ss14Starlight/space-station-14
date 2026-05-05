using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Client.UserInterface.RichText;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using Robust.Shared.IoC;
using Content.Client.Paper.UI;
using Robust.Client.Graphics;

namespace Content.Client.UserInterface.RichText;

/// <summary>
/// Converts [datetime] tags into clickable buttons that autocomplete the in-game date and shift time.
/// </summary>
public sealed class DateTimeTagHandler : IMarkupTagHandler
{
    public string Name => "datetime";
    private static int _dateTimeCounter = 0;

    /// <summary>
    /// Font line height set by PaperWindow to ensure buttons match text height
    /// </summary>
    public static float FontLineHeight { get; set; } = 16.0f; // Default fallback

    private static int GetDateTimeIndex(MarkupNode node) => _dateTimeCounter++;

    /// <summary>
    /// Resets the datetime counter to ensure consistent indexing across renders.
    /// </summary>
    public static void ResetDateTimeCounter() => _dateTimeCounter = 0;

    /// <summary>
    /// Counts datetime buttons before the clicked button to determine which [datetime] tag it represents.
    /// </summary>
    private static int CountDateTimeButtonsBefore(Control clickedButton)
    {
        var count = 0;
        var root = clickedButton;

        // Find the root container
        while (root.Parent != null)
            root = root.Parent;

        // Count datetime buttons in document order
        var found = false;
        CountDateTimeButtonsRecursive(root, clickedButton, ref count, ref found);
        return found ? count : 0;
    }

    private static void CountDateTimeButtonsRecursive(Control control, Control target, ref int count, ref bool found)
    {
        if (found) return;

        if (control is Button btn && btn.Text == Loc.GetString("paper-datetime-button"))
        {
            if (control == target)
            {
                found = true;
                return;
            }
            count++;
        }

        foreach (Control child in control.Children)
        {
            CountDateTimeButtonsRecursive(child, target, ref count, ref found);
        }
    }

    public DateTimeTagHandler()
    {
        IoCManager.InjectDependencies(this);
    }

    public void PushDrawContext(MarkupNode node, MarkupDrawingContext context) { }
    public void PopDrawContext(MarkupNode node, MarkupDrawingContext context) { }
    public string TextBefore(MarkupNode node) => "";
    public string TextAfter(MarkupNode node) => "";

    /// <summary>
    /// Creates a clickable datetime button to replace the [datetime] tag.
    /// </summary>
    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        var btn = new Button
        {
            Text = Loc.GetString("paper-datetime-button"),
            MinSize = new Vector2(120, FontLineHeight + 4),
            MaxSize = new Vector2(120, FontLineHeight + 4),
            Margin = new Thickness(1, 2, 1, 2),
            StyleClasses = { "ButtonSquare" },
            TextAlign = Label.AlignMode.Center
        };

        var dateTimeIndex = GetDateTimeIndex(node);
        btn.Name = $"datetime_{dateTimeIndex}";

        btn.OnPressed += _ =>
        {
            // Find the PaperWindow parent
            var parent = btn.Parent;
            while (parent != null && parent is not PaperWindow)
                parent = parent.Parent;

            if (parent is PaperWindow paperWindow)
            {
                // Count buttons to determine which [datetime] tag this represents
                var buttonIndex = CountDateTimeButtonsBefore(btn);
                // Send datetime request to server instead of handling client-side
                paperWindow.SendDateTimeRequest(buttonIndex);
            }
        };

        control = btn;
        return true;
    }


}
