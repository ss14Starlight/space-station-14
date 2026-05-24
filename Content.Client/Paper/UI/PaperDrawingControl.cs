using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client.Paper.UI;

public sealed class PaperDrawingControl : Control
{
    private sealed class DrawingStroke
    {
        public readonly List<Vector2> Points;
        public readonly float Thickness;
        public readonly Color Color;
        public readonly int ColorNumber;

        public DrawingStroke(List<Vector2> points, float thickness, Color color, int colorNumber)
        {
            Points = points;
            Thickness = thickness;
            Color = color;
            ColorNumber = colorNumber;
        }
    }

    private readonly List<DrawingStroke> _strokes = new();
    private readonly List<Vector2> _currentPoints = new();

    private bool _isDrawing;

    public float LineThickness { get; set; } = 2f;
    public Color LineColor { get; private set; } = Color.Black;
    public int LineColorNumber { get; private set; } = 0;

    public bool EraserEnabled { get; set; }
    public float EraserRadius { get; set; } = 12f;

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

    public void SetLineColor(Color color, string hexColor)
    {
        LineColor = color;
        LineColorNumber = HexToColorNumber(hexColor);
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
            var thickness = 2f;
            var color = Color.Black;
            var colorNumber = 0;
            var pointData = stroke;

            var firstColon = stroke.IndexOf(':');
            if (firstColon > 0)
            {
                var firstPart = stroke.Substring(0, firstColon);

                if (float.TryParse(firstPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedThickness))
                    thickness = Math.Clamp(parsedThickness, 1f, 5f);

                var afterFirst = stroke.Substring(firstColon + 1);
                var secondColon = afterFirst.IndexOf(':');

                if (secondColon > 0)
                {
                    var colorPart = afterFirst.Substring(0, secondColon);

                    if (TryParseColorPart(colorPart, out var parsedColor, out var parsedColorNumber))
                    {
                        color = parsedColor;
                        colorNumber = parsedColorNumber;
                        pointData = afterFirst.Substring(secondColon + 1);
                    }
                    else
                    {
                        pointData = afterFirst;
                    }
                }
                else
                {
                    pointData = afterFirst;
                }
            }

            var points = ParseEncodedPoints(pointData);

            if (points.Count > 1)
                _strokes.Add(new DrawingStroke(points, thickness, color, colorNumber));
        }
    }

    public string GetDrawingData()
    {
        var strokes = new List<DrawingStroke>(_strokes);

        if (_currentPoints.Count > 1)
            strokes.Add(new DrawingStroke(new List<Vector2>(_currentPoints), LineThickness, LineColor, LineColorNumber));

        var strokeStrings = new List<string>();

        foreach (var stroke in strokes)
        {
            if (stroke.Points.Count < 2)
                continue;

            var pointStrings = new List<string>();

            foreach (var point in stroke.Points)
            {
                pointStrings.Add(
                    point.X.ToString("0.####", CultureInfo.InvariantCulture) +
                    "," +
                    point.Y.ToString("0.####", CultureInfo.InvariantCulture));
            }

            var thickness = Math.Clamp(stroke.Thickness, 1f, 5f).ToString("0.##", CultureInfo.InvariantCulture);
            strokeStrings.Add(thickness + ":" + stroke.ColorNumber + ":" + string.Join(';', pointStrings));
        }

        return string.Join('|', strokeStrings);
    }

    public string ExportToSvg()
    {
        const float exportCanvas = 1000f;
        var strokes = new List<DrawingStroke>(_strokes);

        if (_currentPoints.Count > 1)
            strokes.Add(new DrawingStroke(new List<Vector2>(_currentPoints), LineThickness, LineColor, LineColorNumber));

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" viewBox=\"0 0 1000 1000\">");

        foreach (var stroke in strokes)
        {
            if (stroke.Points.Count < 2)
                continue;

            var points = new List<string>();

            foreach (var point in stroke.Points)
            {
                var x = (point.X * exportCanvas).ToString("0.##", CultureInfo.InvariantCulture);
                var y = (point.Y * exportCanvas).ToString("0.##", CultureInfo.InvariantCulture);
                points.Add(x + "," + y);
            }

            var strokeColor = "#" + stroke.ColorNumber.ToString("x6", CultureInfo.InvariantCulture);
            var strokeWidth = (stroke.Thickness * 4f).ToString("0.##", CultureInfo.InvariantCulture);

            sb.AppendLine("  <polyline fill=\"none\" stroke=\"" + strokeColor + "\" stroke-width=\"" + strokeWidth + "\" stroke-linecap=\"round\" stroke-linejoin=\"round\" points=\"" + string.Join(" ", points) + "\" />");
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    public bool ImportFromSvg(string svgText)
    {
        if (string.IsNullOrWhiteSpace(svgText))
            return false;

        var viewBox = ParseViewBox(svgText);
        var imported = new List<DrawingStroke>();

        ImportPolylines(svgText, "polyline", viewBox, imported);
        ImportPolylines(svgText, "polygon", viewBox, imported);
        ImportLines(svgText, viewBox, imported);
        ImportRects(svgText, viewBox, imported);
        ImportCircles(svgText, viewBox, imported);
        ImportEllipses(svgText, viewBox, imported);
        ImportSimplePaths(svgText, viewBox, imported);

        if (imported.Count == 0)
            return false;

        FitImportedStrokesToPage(imported);

        _strokes.Clear();
        _strokes.AddRange(imported);
        _currentPoints.Clear();
        _isDrawing = false;
        return true;
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

        if (EraserEnabled)
        {
            _currentPoints.Clear();
            EraseAt(args.RelativePosition);
            args.Handle();
            return;
        }

        _currentPoints.Clear();
        AddPoint(args.RelativePosition);
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (!EraserEnabled && _isDrawing && _currentPoints.Count > 1)
            _strokes.Add(new DrawingStroke(new List<Vector2>(_currentPoints), LineThickness, LineColor, LineColorNumber));

        _isDrawing = false;
        _currentPoints.Clear();
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (!DrawingEnabled || !_isDrawing)
            return;

        if (EraserEnabled)
        {
            EraseAt(args.RelativePosition);
            args.Handle();
            return;
        }

        AddPoint(args.RelativePosition);
        args.Handle();
    }

    protected override void ControlFocusExited()
    {
        base.ControlFocusExited();

        if (!EraserEnabled && _isDrawing && _currentPoints.Count > 1)
            _strokes.Add(new DrawingStroke(new List<Vector2>(_currentPoints), LineThickness, LineColor, LineColorNumber));

        _isDrawing = false;
        _currentPoints.Clear();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        foreach (var stroke in _strokes)
        {
            DrawStroke(handle, stroke.Points, stroke.Thickness, stroke.Color);
        }

        if (_currentPoints.Count > 1)
            DrawStroke(handle, _currentPoints, LineThickness, LineColor);
    }

    private void DrawStroke(DrawingHandleScreen handle, List<Vector2> points, float thickness, Color color)
    {
        if (points.Count < 2)
            return;

        for (var i = 1; i < points.Count; i++)
        {
            var a = Denormalize(points[i - 1]);
            var b = Denormalize(points[i]);

            DrawThickLine(handle, a, b, thickness, color);
        }
    }

    private void DrawThickLine(DrawingHandleScreen handle, Vector2 a, Vector2 b, float thickness, Color color)
    {
        var radius = Math.Clamp((int)MathF.Round(thickness), 1, 5) - 1;

        handle.DrawLine(a, b, color);

        if (radius <= 0)
            return;

        for (var offset = 1; offset <= radius; offset++)
        {
            handle.DrawLine(a + new Vector2(offset, 0), b + new Vector2(offset, 0), color);
            handle.DrawLine(a + new Vector2(-offset, 0), b + new Vector2(-offset, 0), color);
            handle.DrawLine(a + new Vector2(0, offset), b + new Vector2(0, offset), color);
            handle.DrawLine(a + new Vector2(0, -offset), b + new Vector2(0, -offset), color);
        }
    }

    private void EraseAt(Vector2 relativePosition)
    {
        if (_strokes.Count == 0)
            return;

        // Slightly scale with thickness so thick strokes are easier to erase.
        var radius = Math.Max(EraserRadius, 6f + LineThickness * 3f);

        for (var strokeIndex = _strokes.Count - 1; strokeIndex >= 0; strokeIndex--)
        {
            var stroke = _strokes[strokeIndex];

            if (StrokeIntersectsEraser(stroke, relativePosition, radius))
                _strokes.RemoveAt(strokeIndex);
        }
    }

    private bool StrokeIntersectsEraser(DrawingStroke stroke, Vector2 relativePosition, float radius)
    {
        if (stroke.Points.Count == 0)
            return false;

        if (stroke.Points.Count == 1)
            return Vector2.Distance(Denormalize(stroke.Points[0]), relativePosition) <= radius;

        for (var i = 1; i < stroke.Points.Count; i++)
        {
            var a = Denormalize(stroke.Points[i - 1]);
            var b = Denormalize(stroke.Points[i]);

            var strokeRadius = radius + Math.Max(1f, stroke.Thickness * 2f);

            if (DistancePointToSegment(relativePosition, a, b) <= strokeRadius)
                return true;
        }

        return false;
    }

    private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lengthSquared = ab.LengthSquared();

        if (lengthSquared <= 0.0001f)
            return Vector2.Distance(point, a);

        var t = Vector2.Dot(point - a, ab) / lengthSquared;
        t = Math.Clamp(t, 0f, 1f);

        var closest = a + ab * t;
        return Vector2.Distance(point, closest);
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
            var last = _currentPoints[_currentPoints.Count - 1];

            if (Vector2.DistanceSquared(last, normalized) < 0.00003f)
                return;
        }

        _currentPoints.Add(normalized);
    }

    private Vector2 Denormalize(Vector2 normalized)
    {
        return new Vector2(normalized.X * Size.X, normalized.Y * Size.Y);
    }

    private static List<Vector2> ParseEncodedPoints(string encoded)
    {
        var points = new List<Vector2>();
        var encodedPoints = encoded.Split(';', StringSplitOptions.RemoveEmptyEntries);

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

        return points;
    }

    private sealed class SvgViewBox
    {
        public float MinX;
        public float MinY;
        public float Width;
        public float Height;
    }

    private static SvgViewBox ParseViewBox(string svgText)
    {
        var result = new SvgViewBox
        {
            MinX = 0f,
            MinY = 0f,
            Width = 1000f,
            Height = 1000f
        };

        var match = Regex.Match(svgText, "viewBox\\s*=\\s*[\\\"']([^\\\"']+)[\\\"']", RegexOptions.IgnoreCase);
        if (!match.Success)
            return result;

        var numbers = Regex.Matches(match.Groups[1].Value, "[-+]?\\d*\\.?\\d+(?:[eE][-+]?\\d+)?");
        if (numbers.Count < 4)
            return result;

        if (float.TryParse(numbers[0].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var minX) &&
            float.TryParse(numbers[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var minY) &&
            float.TryParse(numbers[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var width) &&
            float.TryParse(numbers[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
        {
            if (Math.Abs(width) > 0.0001f && Math.Abs(height) > 0.0001f)
            {
                result.MinX = minX;
                result.MinY = minY;
                result.Width = width;
                result.Height = height;
            }
        }

        return result;
    }

    private static void ImportPolylines(string svgText, string tagName, SvgViewBox viewBox, List<DrawingStroke> imported)
    {
        var regex = new Regex("<" + tagName + "\\b([^>]*)>", RegexOptions.IgnoreCase);

        foreach (Match match in regex.Matches(svgText))
        {
            var attributes = match.Groups[1].Value;
            var pointsText = GetAttribute(attributes, "points");

            if (string.IsNullOrWhiteSpace(pointsText))
                continue;

            var points = ParseSvgPointList(pointsText, viewBox);
            if (points.Count <= 1)
                continue;

            if (tagName == "polygon")
                points.Add(points[0]);

            imported.Add(CreateImportedStroke(points, attributes));
        }
    }

    private static void ImportLines(string svgText, SvgViewBox viewBox, List<DrawingStroke> imported)
    {
        var regex = new Regex("<line\\b([^>]*)>", RegexOptions.IgnoreCase);

        foreach (Match match in regex.Matches(svgText))
        {
            var attributes = match.Groups[1].Value;
            var x1 = ParseFloat(GetAttribute(attributes, "x1"), 0f);
            var y1 = ParseFloat(GetAttribute(attributes, "y1"), 0f);
            var x2 = ParseFloat(GetAttribute(attributes, "x2"), 0f);
            var y2 = ParseFloat(GetAttribute(attributes, "y2"), 0f);

            var points = new List<Vector2>
            {
                NormalizeSvgPoint(x1, y1, viewBox),
                NormalizeSvgPoint(x2, y2, viewBox)
            };

            imported.Add(CreateImportedStroke(points, attributes));
        }
    }

    private static void ImportRects(string svgText, SvgViewBox viewBox, List<DrawingStroke> imported)
    {
        var regex = new Regex("<rect\\b([^>]*)>", RegexOptions.IgnoreCase);

        foreach (Match match in regex.Matches(svgText))
        {
            var attributes = match.Groups[1].Value;
            var x = ParseFloat(GetAttribute(attributes, "x"), 0f);
            var y = ParseFloat(GetAttribute(attributes, "y"), 0f);
            var width = ParseFloat(GetAttribute(attributes, "width"), 0f);
            var height = ParseFloat(GetAttribute(attributes, "height"), 0f);

            if (width <= 0f || height <= 0f)
                continue;

            var points = new List<Vector2>
            {
                NormalizeSvgPoint(x, y, viewBox),
                NormalizeSvgPoint(x + width, y, viewBox),
                NormalizeSvgPoint(x + width, y + height, viewBox),
                NormalizeSvgPoint(x, y + height, viewBox),
                NormalizeSvgPoint(x, y, viewBox)
            };

            imported.Add(CreateImportedStroke(points, attributes));
        }
    }

    private static void ImportCircles(string svgText, SvgViewBox viewBox, List<DrawingStroke> imported)
    {
        var regex = new Regex("<circle\\b([^>]*)>", RegexOptions.IgnoreCase);

        foreach (Match match in regex.Matches(svgText))
        {
            var attributes = match.Groups[1].Value;
            var cx = ParseFloat(GetAttribute(attributes, "cx"), 0f);
            var cy = ParseFloat(GetAttribute(attributes, "cy"), 0f);
            var r = ParseFloat(GetAttribute(attributes, "r"), 0f);

            if (r <= 0f)
                continue;

            imported.Add(CreateImportedStroke(ApproximateEllipse(cx, cy, r, r, viewBox), attributes));
        }
    }

    private static void ImportEllipses(string svgText, SvgViewBox viewBox, List<DrawingStroke> imported)
    {
        var regex = new Regex("<ellipse\\b([^>]*)>", RegexOptions.IgnoreCase);

        foreach (Match match in regex.Matches(svgText))
        {
            var attributes = match.Groups[1].Value;
            var cx = ParseFloat(GetAttribute(attributes, "cx"), 0f);
            var cy = ParseFloat(GetAttribute(attributes, "cy"), 0f);
            var rx = ParseFloat(GetAttribute(attributes, "rx"), 0f);
            var ry = ParseFloat(GetAttribute(attributes, "ry"), 0f);

            if (rx <= 0f || ry <= 0f)
                continue;

            imported.Add(CreateImportedStroke(ApproximateEllipse(cx, cy, rx, ry, viewBox), attributes));
        }
    }

    private static void ImportSimplePaths(string svgText, SvgViewBox viewBox, List<DrawingStroke> imported)
    {
        var regex = new Regex("<path\\b([^>]*)>", RegexOptions.IgnoreCase);

        foreach (Match match in regex.Matches(svgText))
        {
            var attributes = match.Groups[1].Value;
            var d = GetAttribute(attributes, "d");

            if (string.IsNullOrWhiteSpace(d))
                continue;

            foreach (var points in ParseSimpleSvgPath(d, viewBox))
            {
                if (points.Count > 1)
                    imported.Add(CreateImportedStroke(points, attributes));
            }
        }
    }

    private static string? GetAttribute(string attributes, string attributeName)
    {
        var match = Regex.Match(attributes, attributeName + "\\s*=\\s*[\\\"']([^\\\"']*)[\\\"']", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static DrawingStroke CreateImportedStroke(List<Vector2> points, string attributes)
    {
        var strokeColorText = GetAttribute(attributes, "stroke");
        if (string.IsNullOrWhiteSpace(strokeColorText) || string.Equals(strokeColorText, "none", StringComparison.OrdinalIgnoreCase))
            strokeColorText = GetAttribute(attributes, "fill");

        if (!TryParseSvgColor(strokeColorText, out var color, out var colorNumber))
        {
            color = Color.Black;
            colorNumber = 0;
        }

        var svgStrokeWidth = ParseFloat(GetAttribute(attributes, "stroke-width"), 8f);
        var thickness = Math.Clamp(svgStrokeWidth / 4f, 1f, 5f);

        return new DrawingStroke(points, thickness, color, colorNumber);
    }

    private static bool TryParseSvgColor(string? colorText, out Color color, out int colorNumber)
    {
        color = Color.Black;
        colorNumber = 0;

        if (string.IsNullOrWhiteSpace(colorText))
            return false;

        var normalized = colorText.Trim();

        if (normalized.StartsWith("#"))
        {
            normalized = normalized.Substring(1);

            if (normalized.Length == 3)
                normalized = "" + normalized[0] + normalized[0] + normalized[1] + normalized[1] + normalized[2] + normalized[2];

            if (normalized.Length == 6)
            {
                try
                {
                    color = Color.FromHex("#" + normalized);
                    colorNumber = HexToColorNumber(normalized);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        if (normalized.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(normalized, "rgb\\s*\\(\\s*(\\d+)\\s*[, ]\\s*(\\d+)\\s*[, ]\\s*(\\d+)\\s*\\)");
            if (match.Success)
            {
                var r = Math.Clamp(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), 0, 255);
                var g = Math.Clamp(int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), 0, 255);
                var b = Math.Clamp(int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture), 0, 255);
                color = new Color(r, g, b);
                colorNumber = (r << 16) | (g << 8) | b;
                return true;
            }
        }

        var lower = normalized.ToLowerInvariant();

        if (lower == "black")
        {
            color = Color.Black;
            colorNumber = 0x000000;
            return true;
        }

        if (lower == "white")
        {
            color = Color.White;
            colorNumber = 0xFFFFFF;
            return true;
        }

        if (lower == "red")
        {
            color = Color.Red;
            colorNumber = 0xFF0000;
            return true;
        }

        if (lower == "green")
        {
            color = Color.Green;
            colorNumber = 0x00FF00;
            return true;
        }

        if (lower == "blue")
        {
            color = Color.Blue;
            colorNumber = 0x0000FF;
            return true;
        }

        if (lower == "yellow")
        {
            color = Color.Yellow;
            colorNumber = 0xFFFF00;
            return true;
        }

        if (lower == "cyan")
        {
            color = Color.Cyan;
            colorNumber = 0x00FFFF;
            return true;
        }

        if (lower == "magenta")
        {
            color = Color.Magenta;
            colorNumber = 0xFF00FF;
            return true;
        }

        return false;
    }

    private static List<Vector2> ParseSvgPointList(string pointsText, SvgViewBox viewBox)
    {
        var matches = Regex.Matches(pointsText, "[-+]?\\d*\\.?\\d+(?:[eE][-+]?\\d+)?");
        var points = new List<Vector2>();

        for (var i = 0; i + 1 < matches.Count; i += 2)
        {
            if (!float.TryParse(matches[i].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(matches[i + 1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                continue;

            points.Add(NormalizeSvgPoint(x, y, viewBox));
        }

        return points;
    }

    private static IEnumerable<List<Vector2>> ParseSimpleSvgPath(string d, SvgViewBox viewBox)
    {
        var tokens = Regex.Matches(d, "[A-Za-z]|[-+]?\\d*\\.?\\d+(?:[eE][-+]?\\d+)?");
        var currentPath = new List<Vector2>();
        var current = Vector2.Zero;
        var start = Vector2.Zero;
        var command = ' ';
        var index = 0;

        while (index < tokens.Count)
        {
            var token = tokens[index].Value;

            if (token.Length == 1 && char.IsLetter(token[0]))
            {
                command = token[0];
                index++;

                if (command == 'Z' || command == 'z')
                {
                    if (currentPath.Count > 1)
                    {
                        currentPath.Add(NormalizeSvgPoint(start.X, start.Y, viewBox));
                        yield return currentPath;
                        currentPath = new List<Vector2>();
                    }
                }

                continue;
            }

            if (command == 'M' || command == 'L')
            {
                if (index + 1 >= tokens.Count)
                    yield break;

                current = new Vector2(
                    float.Parse(tokens[index].Value, CultureInfo.InvariantCulture),
                    float.Parse(tokens[index + 1].Value, CultureInfo.InvariantCulture));

                if (command == 'M' && currentPath.Count > 1)
                {
                    yield return currentPath;
                    currentPath = new List<Vector2>();
                }

                currentPath.Add(NormalizeSvgPoint(current.X, current.Y, viewBox));

                if (command == 'M')
                    start = current;

                command = command == 'M' ? 'L' : command;
                index += 2;
                continue;
            }

            if (command == 'm' || command == 'l')
            {
                if (index + 1 >= tokens.Count)
                    yield break;

                current += new Vector2(
                    float.Parse(tokens[index].Value, CultureInfo.InvariantCulture),
                    float.Parse(tokens[index + 1].Value, CultureInfo.InvariantCulture));

                if (command == 'm' && currentPath.Count > 1)
                {
                    yield return currentPath;
                    currentPath = new List<Vector2>();
                }

                currentPath.Add(NormalizeSvgPoint(current.X, current.Y, viewBox));

                if (command == 'm')
                    start = current;

                command = command == 'm' ? 'l' : command;
                index += 2;
                continue;
            }

            if (command == 'H')
            {
                current.X = float.Parse(tokens[index].Value, CultureInfo.InvariantCulture);
                currentPath.Add(NormalizeSvgPoint(current.X, current.Y, viewBox));
                index++;
                continue;
            }

            if (command == 'h')
            {
                current.X += float.Parse(tokens[index].Value, CultureInfo.InvariantCulture);
                currentPath.Add(NormalizeSvgPoint(current.X, current.Y, viewBox));
                index++;
                continue;
            }

            if (command == 'V')
            {
                current.Y = float.Parse(tokens[index].Value, CultureInfo.InvariantCulture);
                currentPath.Add(NormalizeSvgPoint(current.X, current.Y, viewBox));
                index++;
                continue;
            }

            if (command == 'v')
            {
                current.Y += float.Parse(tokens[index].Value, CultureInfo.InvariantCulture);
                currentPath.Add(NormalizeSvgPoint(current.X, current.Y, viewBox));
                index++;
                continue;
            }

            index++;
        }

        if (currentPath.Count > 1)
            yield return currentPath;
    }

    private static List<Vector2> ApproximateEllipse(float cx, float cy, float rx, float ry, SvgViewBox viewBox)
    {
        var points = new List<Vector2>();
        const int segments = 24;

        for (var i = 0; i <= segments; i++)
        {
            var angle = (MathF.Tau * i) / segments;
            var x = cx + MathF.Cos(angle) * rx;
            var y = cy + MathF.Sin(angle) * ry;
            points.Add(NormalizeSvgPoint(x, y, viewBox));
        }

        return points;
    }

    private static Vector2 NormalizeSvgPoint(float x, float y, SvgViewBox viewBox)
    {
        var normalizedX = viewBox.Width == 0f ? 0f : (x - viewBox.MinX) / viewBox.Width;
        var normalizedY = viewBox.Height == 0f ? 0f : (y - viewBox.MinY) / viewBox.Height;

        // Do not clamp here.
        // We fit all imported strokes to the paper after parsing, so oversized SVGs still fit.
        return new Vector2(normalizedX, normalizedY);
    }

    private static void FitImportedStrokesToPage(List<DrawingStroke> imported)
    {
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;

        foreach (var stroke in imported)
        {
            foreach (var point in stroke.Points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        if (minX == float.MaxValue || minY == float.MaxValue || maxX == float.MinValue || maxY == float.MinValue)
            return;

        var sourceWidth = Math.Max(maxX - minX, 0.0001f);
        var sourceHeight = Math.Max(maxY - minY, 0.0001f);

        const float padding = 0.05f;
        const float usableSize = 1f - padding * 2f;

        var scale = Math.Min(usableSize / sourceWidth, usableSize / sourceHeight);
        var fittedWidth = sourceWidth * scale;
        var fittedHeight = sourceHeight * scale;

        var offsetX = (1f - fittedWidth) / 2f;
        var offsetY = (1f - fittedHeight) / 2f;

        foreach (var stroke in imported)
        {
            for (var i = 0; i < stroke.Points.Count; i++)
            {
                var point = stroke.Points[i];

                stroke.Points[i] = new Vector2(
                    Math.Clamp(((point.X - minX) * scale) + offsetX, padding, 1f - padding),
                    Math.Clamp(((point.Y - minY) * scale) + offsetY, padding, 1f - padding));
            }
        }
    }

    private static float ParseFloat(string? value, float fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var match = Regex.Match(value, "[-+]?\\d*\\.?\\d+(?:[eE][-+]?\\d+)?");
        if (!match.Success)
            return fallback;

        return float.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static bool TryParseColorPart(string colorPart, out Color color, out int colorNumber)
    {
        color = Color.Black;
        colorNumber = 0;

        if (int.TryParse(colorPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedNumber))
        {
            parsedNumber = Math.Clamp(parsedNumber, 0, 0xFFFFFF);
            colorNumber = parsedNumber;
            color = Color.FromHex("#" + parsedNumber.ToString("x6", CultureInfo.InvariantCulture));
            return true;
        }

        if (colorPart.Length == 6)
        {
            try
            {
                color = Color.FromHex("#" + colorPart);
                colorNumber = HexToColorNumber(colorPart);
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static int HexToColorNumber(string hex)
    {
        if (hex.StartsWith("#"))
            hex = hex.Substring(1);

        if (hex.Length != 6)
            return 0;

        if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return Math.Clamp(value, 0, 0xFFFFFF);

        return 0;
    }
}
