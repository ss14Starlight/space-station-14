using System.Globalization;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client.Paper.UI;

public sealed class PaperDrawingControl : Control
{
    private readonly List<List<Vector2>> _strokes = new();
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

    public void SetDrawingData(string drawingData)
    {
        if (_isDrawing)
            return;

        _strokes.Clear();

        if (string.IsNullOrEmpty(drawingData))
            return;

        var strokes = drawingData.Split('|', StringSplitOptions.RemoveEmptyEntries);

        foreach (var stroke in strokes)
        {
            var points = new List<Vector2>();
            var encodedPoints = stroke.Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var encodedPoint in encodedPoints)
            {
                var values = encodedPoint.Split(',', StringSplitOptions.RemoveEmptyEntries);

                if (values.Length != 2)
                    continue;

                if (!float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
                    continue;

                if (!float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                    continue;

                points.Add(new Vector2(
                    Math.Clamp(x, 0f, 1f),
                    Math.Clamp(y, 0f, 1f)));
            }

            if (points.Count > 1)
                _strokes.Add(points);
        }
    }

    public string GetDrawingData()
    {
        var strokes = new List<List<Vector2>>(_strokes);

        if (_currentPoints.Count > 1)
            strokes.Add(new List<Vector2>(_currentPoints));

        var strokeStrings = new List<string>();

        foreach (var stroke in strokes)
        {
            if (stroke.Count < 2)
                continue;

            var pointStrings = new List<string>();

            foreach (var point in stroke)
            {
                pointStrings.Add(
                    point.X.ToString("0.####", CultureInfo.InvariantCulture) +
                    "," +
                    point.Y.ToString("0.####", CultureInfo.InvariantCulture));
            }

            strokeStrings.Add(string.Join(';', pointStrings));
        }

        return string.Join('|', strokeStrings);
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
            _strokes.Add(new List<Vector2>(_currentPoints));

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
            _strokes.Add(new List<Vector2>(_currentPoints));

        _isDrawing = false;
        _currentPoints.Clear();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        foreach (var stroke in _strokes)
        {
            DrawStroke(handle, stroke);
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

            if (Vector2.DistanceSquared(last, normalized) < 0.00003f)
                return;
        }

        _currentPoints.Add(normalized);

        if (_currentPoints.Count > 128)
            _currentPoints.RemoveAt(0);
    }

    private Vector2 Denormalize(Vector2 normalized)
    {
        return new Vector2(normalized.X * Size.X, normalized.Y * Size.Y);
    }
}
