using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.UserInterface.Controls;

/// <summary>
/// A row of joined toggle buttons with one picked, for short choices that don't need a dropdown.
/// Mirrors the <see cref="OptionButton"/> methods, so it can replace one without touching the code around it.
/// </summary>
/// <remarks>
/// No <see cref="ButtonGroup"/>: a button leaves its group when it leaves the UI tree and never rejoins,
/// so a reopened window ends up with several buttons lit. The pressed state is set by hand instead.
/// </remarks>
public sealed class SegmentedButtons : BoxContainer
{
    public const string StyleClassSegment = "Segment";

    private readonly Dictionary<int, Button> _buttons = new();

    public event Action<SegmentSelectedEventArgs>? OnItemSelected;

    public int SelectedId { get; private set; } = -1;

    public SegmentedButtons()
    {
        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 0;
    }

    /// <summary>
    /// Adds a button at the end of the row. Nothing is picked until <see cref="SelectId"/> is called.
    /// </summary>
    public void AddItem(string text, int id)
    {
        var button = new Button
        {
            Text = text,
            ToggleMode = true,
            StyleClasses = { StyleClassSegment },
        };
        button.OnPressed += _ =>
        {
            SelectId(id);
            OnItemSelected?.Invoke(new SegmentSelectedEventArgs(id));
        };
        _buttons[id] = button;
        AddChild(button);
        UpdateEnds();
    }

    /// <summary>
    /// Lights the button with this id and unlights the rest. An unknown id is ignored and changes nothing.
    /// Doesn't raise <see cref="OnItemSelected"/>: that's only for the player pressing a button.
    /// </summary>
    public void SelectId(int id)
    {
        if (!_buttons.ContainsKey(id))
            return;

        SelectedId = id;
        foreach (var (buttonId, button) in _buttons)
            button.Pressed = buttonId == id;
    }

    /// <summary>
    /// Removes every button and resets <see cref="SelectedId"/> to -1.
    /// </summary>
    public void Clear()
    {
        RemoveAllChildren();
        _buttons.Clear();
        SelectedId = -1;
    }

    /// <summary>
    /// Only the outer buttons keep rounded ends.
    /// </summary>
    private void UpdateEnds()
    {
        var index = 0;
        foreach (var button in _buttons.Values)
        {
            button.RemoveStyleClass("OpenLeft");
            button.RemoveStyleClass("OpenRight");
            button.RemoveStyleClass("OpenBoth");
            if (_buttons.Count > 1)
            {
                button.AddStyleClass(index == 0 ? "OpenRight" : index == _buttons.Count - 1 ? "OpenLeft" : "OpenBoth");
            }
            index++;
        }
    }
}

public readonly record struct SegmentSelectedEventArgs(int Id);
