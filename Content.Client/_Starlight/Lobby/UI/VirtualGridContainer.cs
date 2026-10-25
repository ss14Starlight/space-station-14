// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;

namespace Content.Client._Starlight.Lobby.UI;

public sealed class VirtualGridContainer : VirtualItemsContainer
{
    private const float ScrollbarReserve = 14f;
    private const float MinCellSize = 32f;

    public int Columns
    {
        get;
        set
        {
            field = Math.Max(1, value);
            InvalidateMeasure();
            RecomputeItemSize();
        }
    } = 2;

    public int RowCount => (TotalItemCount + Columns - 1) / Columns;

    protected override int ItemsPerScrollLine => Columns;

    public VirtualGridContainer() => OnResized += RecomputeItemSize;

    protected override Vector2 GetTotalSize()
    {
        var rows = RowCount;
        var totalWidth = (Columns * ItemSize.X) + Math.Max(0, Columns - 1) * Separation;
        var height = (rows * ItemSize.Y) + (Math.Max(0, rows - 1) * Separation);
        return new Vector2(totalWidth, height);
    }

    protected override Vector2 GetItemPosition(int absoluteIndex)
    {
        var col = absoluteIndex % Columns;
        var row = absoluteIndex / Columns;
        return new Vector2(
            col * (ItemSize.X + Separation),
            row * (ItemSize.Y + Separation));
    }

    private void RecomputeItemSize()
    {
        var width = Width;
        if (width <= 0)
            return;

        var totalSep = MathF.Max(0, Columns - 1) * Separation;
        var cell = MathF.Max(MinCellSize, MathF.Floor((width - ScrollbarReserve - totalSep) / Columns));
        ItemSize = new Vector2(cell, cell);
    }
}
