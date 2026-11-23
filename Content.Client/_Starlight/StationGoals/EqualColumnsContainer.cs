using System.Linq;
using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.StationGoals;

/// <summary>
/// Lays out visible children side by side in columns of equal width, so wrapping text cannot make one column wider.
/// Every child gets the height of the tallest one.
/// </summary>
public sealed class EqualColumnsContainer : Container
{
    /// <summary>
    /// Gap between columns.
    /// </summary>
    public float Separation { get; set; } = 8;

    /// <summary>
    /// The number of columns. Zero uses one column per visible child.
    /// </summary>
    public int Columns { get; set; }

    private int ColumnCount(int visible) => Columns > 0 ? Columns : Math.Max(1, visible);

    private float ColumnWidth(float width, int columns) => Math.Max(0, (width - (Separation * (columns - 1))) / columns);

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var visible = Children.Count(c => c.Visible);
        var columns = ColumnCount(visible);
        var width = float.IsPositiveInfinity(availableSize.X) ? 300 * columns : availableSize.X;
        var columnWidth = ColumnWidth(width, columns);

        var height = 0f;
        foreach (var child in Children)
        {
            if (!child.Visible)
                continue;

            child.Measure(new Vector2(columnWidth, availableSize.Y));
            height = Math.Max(height, child.DesiredSize.Y);
        }

        return new Vector2(width, height);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var columns = ColumnCount(Children.Count(c => c.Visible));
        var columnWidth = ColumnWidth(finalSize.X, columns);

        var x = 0f;
        foreach (var child in Children)
        {
            if (!child.Visible)
                continue;

            child.Arrange(UIBox2.FromDimensions(new Vector2(x, 0), new Vector2(columnWidth, finalSize.Y)));
            x += columnWidth + Separation;
        }

        return finalSize;
    }
}
