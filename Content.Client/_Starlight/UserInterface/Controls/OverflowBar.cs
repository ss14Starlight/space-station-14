using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.UserInterface.Controls;

/// <summary>
/// A horizontal bar that automatically moves items into a dropdown menu when there isn't enough space to display them all.
/// </summary>
public sealed class OverflowBar : Control
{
    public float Separation { get; set; } = 2;

    private enum Slot : byte
    {
        Bar,
        Menu,
        None,
    }

    private readonly DropdownButton _more = new();
    private readonly BoxContainer _menu = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, MinWidth = 180 };
    private readonly List<Control> _items = new();
    private readonly Dictionary<Control, BarPlacement> _placements = new();
    private readonly Dictionary<Control, Slot> _slots = new();
    private readonly Dictionary<Control, float> _barWidths = new();
    private Dictionary<Control, Slot>? _pending;

    private float _moreWidth;

    public OverflowBar()
    {
        _more.Text = "•••";
        _more.ToolTip = Loc.GetString("overflow-bar-more");
        _more.SetContent(_menu);
    }

    public void Setup()
    {
        _items.AddRange(Children.Where(c => c != _more));
        AddChild(_more);

        foreach (var item in _items)
        {
            _slots[item] = Slot.Bar;
            item.OnVisibilityChanged += _ => InvalidateMeasure();
            if (item is BaseButton button)
                _more.RegisterItem(button);
        }
    }

    /// <summary>
    /// Sets the placement of each item in the bar. Items with a placement of <see cref="BarPlacement.Auto"/> will be moved to the menu when there isn't enough space to display them all, while items with a placement of <see cref="BarPlacement.Pinned"/> will always remain in the bar. Items with a placement of <see cref="BarPlacement.Menu"/> will always be moved to the menu, and items with a placement of <see cref="BarPlacement.Hidden"/> will be hidden entirely.
    /// </summary>
    public void SetPlacements(IReadOnlyDictionary<string, BarPlacement> placements)
    {
        foreach (var item in _items)
            _placements[item] = item.Name is { } name ? placements.GetValueOrDefault(name, BarPlacement.Auto) : BarPlacement.Auto;

        InvalidateMeasure();
    }

    private BarPlacement Placement(Control item)
        => _placements.GetValueOrDefault(item, BarPlacement.Auto);

    private bool OnBarCandidate(Control item)
        => item.Visible && Placement(item) is BarPlacement.Pinned or BarPlacement.Auto;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = 0f;
        var height = 0f;
        foreach (var item in _items)
        {
            if (!item.Visible)
                continue;

            if (_slots[item] == Slot.Bar)
            {
                item.Measure(new Vector2(float.PositiveInfinity, availableSize.Y));
                _barWidths[item] = item.DesiredSize.X;
                height = Math.Max(height, item.DesiredSize.Y);
            }

            if (OnBarCandidate(item))
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
        var slots = PickSlots(finalSize.X);
        if (_pending == null && slots.Any(s => _slots[s.Key] != s.Value))
        {
            _pending = slots;
            UserInterfaceManager.DeferAction(ApplyPending);
        }

        var x = 0f;
        foreach (var item in _items)
        {
            if (!item.Visible || _slots[item] != Slot.Bar)
                continue;

            var width = BarWidth(item);
            item.Arrange(UIBox2.FromDimensions(new Vector2(x, 0), new Vector2(width, finalSize.Y)));
            x += width + Separation;
        }

        _more.Visible = _items.Any(i => i.Visible && _slots[i] == Slot.Menu);
        _more.Arrange(UIBox2.FromDimensions(new Vector2(finalSize.X - _moreWidth, 0), new Vector2(_moreWidth, finalSize.Y)));

        return finalSize;
    }

    private Dictionary<Control, Slot> PickSlots(float available)
    {
        var slots = new Dictionary<Control, Slot>();
        foreach (var item in _items)
        {
            slots[item] = Placement(item) switch
            {
                BarPlacement.Hidden => Slot.None,
                BarPlacement.Menu => Slot.Menu,
                _ => Slot.Bar,
            };
        }

        var candidates = _items.Where(OnBarCandidate).ToList();
        var menuNeeded = _items.Any(i => i.Visible && Placement(i) == BarPlacement.Menu);
        if (candidates.Sum(i => BarWidth(i) + Separation) + (menuNeeded ? _moreWidth : 0) <= available)
            return slots;

        var used = _moreWidth + candidates.Where(i => Placement(i) == BarPlacement.Pinned).Sum(i => BarWidth(i) + Separation);
        var full = false;
        foreach (var item in candidates.Where(i => Placement(i) == BarPlacement.Auto))
        {
            used += BarWidth(item) + Separation;
            full |= used > available;
            if (full)
                slots[item] = Slot.Menu;
        }

        return slots;
    }

    private void ApplyPending()
    {
        if (_pending is not { } slots)
            return;

        _pending = null;
        foreach (var item in _items)
        {
            var slot = slots[item];
            if (slot == _slots[item])
                continue;

            _slots[item] = slot;
            item.Orphan();
            if (slot == Slot.Bar)
                AddChild(item);
            else if (slot == Slot.Menu)
                _menu.AddChild(item);

            if (item is MenuButton menuButton)
                menuButton.ShowName = slot == Slot.Menu;
            if (slot == Slot.Menu)
                item.AddStyleClass(DropdownButton.StyleClassDropdownItem);
            else
                item.RemoveStyleClass(DropdownButton.StyleClassDropdownItem);
        }

        foreach (var item in _items.Where(i => _slots[i] == Slot.Menu))
            item.SetPositionLast();
        _more.SetPositionLast();
        InvalidateMeasure();
    }

    private float BarWidth(Control item)
        => _barWidths.GetValueOrDefault(item, item.MinWidth);
}
