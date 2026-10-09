using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.UserInterface.Controls;

/// <summary>
/// A control that allows switching between multiple sections of content, each represented by a button in a navigation panel.
/// </summary>
public sealed class SectionNav : BoxContainer
{
    public const string StyleClassNavPanel = "SectionNavPanel";
    public const string StyleClassNavButton = "SectionNavButton";

    private readonly BoxContainer _buttons;
    private readonly Control _content;
    private readonly ButtonGroup _group = new();
    private readonly List<(Control Page, Button Button)> _sections = new();
    private int _current = -1;

    public SectionNav()
    {
        Orientation = LayoutOrientation.Horizontal;

        var panel = new PanelContainer { MinWidth = 160 };
        panel.AddStyleClass(StyleClassNavPanel);
        _buttons = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(0, 6) };
        panel.AddChild(_buttons);
        AddChild(panel);

        _content = new Control { HorizontalExpand = true, VerticalExpand = true, Margin = new Thickness(8, 0, 0, 0) };
        AddChild(_content);
    }

    public int CurrentSection
    {
        get => _current;
        set => Select(value);
    }

    public void AddSection(Control page, string title)
    {
        page.Orphan();
        page.Visible = false;
        _content.AddChild(page);

        var index = _sections.Count;
        var button = new Button { Text = title, ToggleMode = true, Group = _group };
        button.AddStyleClass(StyleClassNavButton);
        button.OnPressed += _ => Select(index);
        _buttons.AddChild(button);
        _sections.Add((page, button));

        if (_current == -1)
            Select(index);
    }

    public void SetSectionVisible(Control page, bool visible)
    {
        var index = _sections.FindIndex(s => s.Page == page);
        if (index == -1)
            return;

        _sections[index].Button.Visible = visible;
        if (!visible && index == _current)
            Select(_sections.FindIndex(s => s.Button.Visible));
    }

    private void Select(int index)
    {
        if (index < 0 || index >= _sections.Count)
            return;

        _current = index;
        for (var i = 0; i < _sections.Count; i++)
        {
            _sections[i].Page.Visible = i == index;
            _sections[i].Button.Pressed = i == index;
        }
    }
}
