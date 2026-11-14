using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Lobby.UI;

/// <summary>
/// A titled card with a text box, for the free-text parts of a character: descriptions and notes.
/// Laid out like the entries of the jobs, antags and traits tabs.
/// </summary>
public sealed class NoteField : PanelContainer
{
    private readonly Label _title;
    private readonly Label _count;
    private readonly VScrollBar? _scrollBar;

    public readonly TextEdit Input;

    public string? Title
    {
        get => _title.Text;
        set => _title.Text = value;
    }

    /// <summary>
    /// Locale id of the grey text shown while the box is empty.
    /// </summary>
    public string? PlaceholderLoc
    {
        set => Input.Placeholder = value == null ? null : new Rope.Leaf(Loc.GetString(value));
    }

    public NoteField()
    {
        HorizontalExpand = true;
        StyleClasses.Add("TraitsEntryPanel");

        _title = new Label { StyleClasses = { "TraitsEntryNameLabel" }, HorizontalExpand = true };
        _count = new Label { StyleClasses = { StyleClass.LabelSubText } };
        Input = new TextEdit { MinHeight = 110, HorizontalExpand = true, VerticalExpand = true };
        Input.OnTextChanged += _ => UpdateCount();

        AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(10, 8),
            SeparationOverride = 6,
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children = { _title, _count },
                },
                Input,
            },
        });

        // The text box keeps its scrollbar private; it's found among the children to hide it while there's nothing to scroll.
        foreach (var child in Input.Children)
        {
            if (child is VScrollBar bar)
                _scrollBar = bar;
        }
    }

    public void UpdateCount()
    {
        var length = Input.TextLength;
        _count.Text = length > 0 ? Loc.GetString("character-info-length", ("count", length)) : string.Empty;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _scrollBar?.Visible = _scrollBar.MaxValue > _scrollBar.Page + 1f;
    }
}
