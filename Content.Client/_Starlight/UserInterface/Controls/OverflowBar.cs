using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.UserInterface.Controls;

/// <summary>
/// A horizontal bar that displays a set of controls, and moves any that don't fit into a dropdown menu.
/// </summary>
public sealed class OverflowBar : Control
{
    public float Separation { get; set; } = 2;

    private readonly DropdownButton _more = new();
    private readonly BoxContainer _menu = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, MinWidth = 180 };
    private readonly List<Control> _items = new();
    private readonly HashSet<Control> _menuOnly = new();
    private readonly Dictionary<Control, float> _barWidths = new();
    private readonly HashSet<Control> _inMenu = new();
    private HashSet<Control>? _pendingMenu;

    private float _moreWidth;

    public OverflowBar()
    {
        _more.Text = "•••";
        _more.ToolTip = Loc.GetString("overflow-bar-more");
        _more.SetContent(_menu);
    }

    public void Setup(params Control[] menuOnly)
    {
        _items.AddRange(Children.Where(c => c != _more));
        _menuOnly.UnionWith(menuOnly);
        AddChild(_more);

        foreach (var item in _items)
        {
            item.OnVisibilityChanged += _ => InvalidateMeasure();
            if (item is BaseButton button)
                _more.RegisterItem(button);
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = 0f;
        var height = 0f;
        foreach (var item in _items)
        {
            if (!item.Visible)
                continue;

            if (!_inMenu.Contains(item))
            {
                item.Measure(new Vector2(float.PositiveInfinity, availableSize.Y));
                _barWidths[item] = item.DesiredSize.X;
                height = Math.Max(height, item.DesiredSize.Y);
            }

            if (!_menuOnly.Contains(item))
                width += BarWidth(item) + Separation;
        }

        _more.Measure(new Vector2(float.PositiveInfinity, availableSize.Y));
        _moreWidth = Math.Max(_moreWidth, _more.DesiredSize.X);
        width += _moreWidth;
        height = Math.Max(height, _more.DesiredSize.Y);

        return new Vector2(Math.Min(width, availableSize.X), height);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var menu = PickMenuItems(finalSize.X);
        if (!menu.SetEquals(_inMenu) && _pendingMenu == null)
        {
            _pendingMenu = menu;
            UserInterfaceManager.DeferAction(ApplyPending);
        }

        var x = 0f;
        foreach (var item in _items)
        {
            if (!item.Visible || _inMenu.Contains(item))
                continue;

            var width = BarWidth(item);
            item.Arrange(UIBox2.FromDimensions(new Vector2(x, 0), new Vector2(width, finalSize.Y)));
            x += width + Separation;
        }

        _more.Visible = _items.Any(i => i.Visible && _inMenu.Contains(i));
        _more.Arrange(UIBox2.FromDimensions(new Vector2(finalSize.X - _moreWidth, 0), new Vector2(_moreWidth, finalSize.Y)));

        return finalSize;
    }

    private HashSet<Control> PickMenuItems(float available)
    {
        var menu = new HashSet<Control>(_menuOnly);
        var barItems = _items.Where(i => i.Visible && !_menuOnly.Contains(i)).ToList();

        var total = barItems.Sum(i => BarWidth(i) + Separation);
        var menuNeeded = _items.Any(i => i.Visible && _menuOnly.Contains(i));
        if (total + (menuNeeded ? _moreWidth : 0) <= available)
            return menu;

        var room = available - _moreWidth;
        var used = 0f;
        var full = false;
        foreach (var item in barItems)
        {
            used += BarWidth(item) + Separation;
            full |= used > room;
            if (full)
                menu.Add(item);
        }

        return menu;
    }

    private void ApplyPending()
    {
        if (_pendingMenu is not { } menu)
            return;

        _pendingMenu = null;
        foreach (var item in _items)
        {
            var toMenu = menu.Contains(item);
            if (toMenu == _inMenu.Contains(item))
                continue;

            item.Orphan();
            if (toMenu)
            {
                _inMenu.Add(item);
                _menu.AddChild(item);
            }
            else
            {
                _inMenu.Remove(item);
                AddChild(item);
            }

            if (item is MenuButton menuButton)
                menuButton.ShowName = toMenu;
            if (toMenu)
                item.AddStyleClass(DropdownButton.StyleClassDropdownItem);
            else
                item.RemoveStyleClass(DropdownButton.StyleClassDropdownItem);
        }

        foreach (var item in _items.Where(_inMenu.Contains))
            item.SetPositionLast();
        _more.SetPositionLast();
        InvalidateMeasure();
    }

    private float BarWidth(Control item)
        => _barWidths.GetValueOrDefault(item, item.MinWidth);
}
