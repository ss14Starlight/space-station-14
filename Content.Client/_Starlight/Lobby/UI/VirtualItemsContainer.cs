using System;
using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._Starlight.Lobby.UI;

public abstract class VirtualItemsContainer : Container
{
    private Vector2 _itemSize = new(140, 140);
    private Func<int, Control>? _itemFactory;
    private (int Start, int End) _visibleRange = (0, -1);
    private ScrollContainer? _scrollContainer;

    public int TotalItemCount
    {
        get;
        private set { field = value; InvalidateMeasure(); }
    }

    public int ItemOffset
    {
        get;
        private set { field = value; InvalidateArrange(); }
    }

    public float Separation
    {
        get;
        set { field = value; InvalidateMeasure(); }
    } = 4;

    public Vector2 ItemSize
    {
        get => _itemSize;
        set
        {
            if (value == _itemSize)
                return;
            _itemSize = value;
            InvalidateMeasure();
            InvalidateVisibleRange();
            Refresh();
        }
    }

    protected abstract Vector2 GetTotalSize();

    protected abstract Vector2 GetItemPosition(int absoluteIndex);

    protected virtual int ItemsPerScrollLine => 1;

    public void SetItems(int count, Func<int, Control> factory)
    {
        _itemFactory = factory;
        TotalItemCount = count;
        ItemOffset = 0;
        InvalidateVisibleRange();
        _scrollContainer?.SetScrollValue(Vector2.Zero);
        Refresh();
    }

    public void RefreshAll()
    {
        InvalidateVisibleRange();
        Refresh();
    }

    public void Refresh()
    {
        if (_itemFactory == null || TotalItemCount == 0)
        {
            RemoveAllChildren();
            _visibleRange = (0, -1);
            return;
        }

        var rowStride = _itemSize.Y + Separation;
        if (rowStride <= 0)
            return;

        var perLine = Math.Max(1, ItemsPerScrollLine);
        var viewportHeight = _scrollContainer?.Height ?? Height;
        var scrollY = _scrollContainer != null ? MathF.Max(0, _scrollContainer.GetScrollValue().Y) : 0f;

        var startLine = (int)MathF.Floor(scrollY / rowStride);
        var visibleLines = (int)MathF.Ceiling(viewportHeight / rowStride) + 1;
        var endLine = startLine + visibleLines;

        var startIndex = Math.Max(0, startLine * perLine);
        var endIndex = Math.Min(TotalItemCount - 1, (endLine + 1) * perLine - 1);

        if (startIndex == _visibleRange.Start && endIndex == _visibleRange.End)
            return;

        RemoveAllChildren();
        ItemOffset = startIndex;
        for (var i = startIndex; i <= endIndex; i++)
            AddChild(_itemFactory(i));

        _visibleRange = (startIndex, endIndex);
    }

    protected void InvalidateVisibleRange() => _visibleRange = (-1, -2);

    protected override void EnteredTree()
    {
        base.EnteredTree();
        AttachToScrollContainer(FindParentScrollContainer());
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        AttachToScrollContainer(null);
    }

    private void AttachToScrollContainer(ScrollContainer? scroll)
    {
        if (_scrollContainer == scroll)
            return;

        if (_scrollContainer != null)
        {
            _scrollContainer.OnScrolled -= Refresh;
            _scrollContainer.OnResized -= Refresh;
        }

        _scrollContainer = scroll;

        if (_scrollContainer != null)
        {
            _scrollContainer.OnScrolled += Refresh;
            _scrollContainer.OnResized += Refresh;
        }
    }

    private ScrollContainer? FindParentScrollContainer()
    {
        for (var p = Parent; p != null; p = p.Parent)
        {
            if (p is ScrollContainer sc)
                return sc;
        }
        return null;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        foreach (var child in Children)
            child.Measure(_itemSize);

        return GetTotalSize();
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var index = ItemOffset;
        foreach (var child in Children)
        {
            var pos = GetItemPosition(index);
            child.Arrange(UIBox2.FromDimensions(pos.X, pos.Y, _itemSize.X, _itemSize.Y));
            index++;
        }

        return finalSize;
    }
}
