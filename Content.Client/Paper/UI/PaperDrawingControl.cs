using System.Numerics;
using Content.Shared.Paper;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using static Content.Shared.Paper.PaperComponent;

namespace Content.Client.Paper.UI;

public sealed class PaperDrawingControl : Control
{
    private readonly List<PaperDrawingStroke> _strokes = new();
    private readonly List<Vector2> _currentPoints = new();

    private bool _isDrawing;

    public bool DrawingEnabled
    {
        get => MouseFilter == MouseFilterMode.Stop;
        set
        {
            MouseFilter = value ? MouseFilterMode.Stop : MouseFilterMode.Ignore;

            if (!value)
            {
                _isDrawing = false;
                _currentPoints.Clear();
            }
        }
    }

    public PaperDrawingControl()
    {
        RectClipContent = true;
        DrawingEnabled = false;
    }

    public void SetDrawing(List<PaperDrawingStroke> drawing)
    {
        if (_isDrawing)
            return;

        _strokes.Clear();
        _strokes.AddRange(drawing);
    }

    public List<PaperDrawingStroke> GetDrawing()
    {
        var drawing = new List<PaperDrawingStroke>(_strokes);

        if (_currentPoints.Count > 1)
            drawing.Add(new PaperDrawingStroke(new List<Vector2>(_currentPoints), 2f));

        return drawing;
    }

    public void ClearDrawing()
    {
        _strokes.Clear();
        _currentPoints.Clear();
        _isDrawing = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (!DrawingEnabled || args.Function != EngineKeyFunctions.UIClick)
            return;

        _isDrawing = true;
        _currentPoints.Clear();
        AddPoint(args.RelativePosition);
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (_isDrawing && _currentPoints.Count > 1)
            _strokes.Add(new PaperDrawingStroke(new List<Vector2>(_currentPoints), 2f));

        _isDrawing = false;
        _currentPoints.Clear();
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (!DrawingEnabled || !_isDrawing)
            return;

        AddPoint(args.RelativePosition);
        args.Handle();
    }

    protected override void ControlFocusExited()
    {
        base.ControlFocusExited();

        if (_isDrawing && _currentPoints.Count > 1)
            _strokes.Add(new PaperDrawingStroke(new List<Vector2>(_currentPoints), 2f));

        _isDrawing = false;
        _currentPoints.Clear();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        foreach (var stroke in _strokes)
        {
            DrawStroke(handle, stroke.Points);
        }

        if (_currentPoints.Count > 1)
            DrawStroke(handle, _currentPoints);
    }

    private void DrawStroke(DrawingHandleScreen handle, List<Vector2> points)
    {
        if (points.Count < 2)
            return;

        for (var i = 1; i < points.Count; i++)
        {
            var a = Denormalize(points[i - 1]);
            var b = Denormalize(points[i]);

            // This Robust fork only has the 3-argument DrawLine overload.
            handle.DrawLine(a, b, Color.Black);
        }
    }

    private void AddPoint(Vector2 relativePosition)
    {
        if (Size.X <= 0 || Size.Y <= 0)
            return;

        var normalized = new Vector2(
            Math.Clamp(relativePosition.X / Size.X, 0f, 1f),
            Math.Clamp(relativePosition.Y / Size.Y, 0f, 1f));

        if (_currentPoints.Count > 0)
        {
            var last = _currentPoints[^1];

            // Avoid generating hundreds of near-identical points while the mouse barely moves.
            if (Vector2.DistanceSquared(last, normalized) < 0.00003f)
                return;
        }

        _currentPoints.Add(normalized);

        // Same limit as the shared/server validation.
        if (_currentPoints.Count > 128)
            _currentPoints.RemoveAt(0);
    }

    private Vector2 Denormalize(Vector2 normalized)
    {
        return new Vector2(normalized.X * Size.X, normalized.Y * Size.Y);
    }
}
