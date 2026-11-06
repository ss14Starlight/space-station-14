using System.Numerics;
using Content.Client.Pinpointer.UI;
using Content.Shared._Starlight.Computers.TacticalPlanning;
using Content.Shared.Pinpointer;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Physics.Components;

namespace Content.Client._Starlight.Computers.TacticalPlanning.UI;

/// <summary>
/// Adds planning ink to the station map while retaining its normal left-drag pan and wheel zoom.
/// </summary>
public sealed class TacticalPlanningMapControl : NavMapControl
{
    // The tactical window owns its toolbar; the inherited fixed-width header should not
    // constrain this map when the window is resized.
    protected override float TopPanelWidth => float.NaN;

    private const float InitialPointSpacing = 0.25f;
    private const float IconSize = 24f;
    private const float IconHitPadding = 6f;
    private const int MaxStrokePoints = TacticalPlanDrawingValidation.MaxPointsPerStroke;

    private readonly List<TacticalPlanAnnotation> _annotations = new();
    private readonly List<Vector2> _pendingStroke = new();
    private bool _rightHeld;
    private bool _rightMoved;
    private Vector2 _rightDownPosition;
    private float _pointSpacing = InitialPointSpacing;
    private Vector2 _leftDownPosition;
    private int? _selectedId;
    private bool _syndicatePalette;

    public TacticalPlanTool Tool = TacticalPlanTool.Draw;
    public bool CanEdit;
    public Color InkColor = Color.OrangeRed;
    public TacticalPlanIcon SelectedIcon = TacticalPlanIcon.Nuke;

    public event Action<Vector2>? AnnotationInspected;
    public event Action<TacticalPlanAnnotationKind, List<Vector2>>? AnnotationAdded;
    public event Action<Vector2, TacticalPlanIcon>? IconAdded;
    public event Action<Vector2>? AnnotationErased;

    /// <summary>Enables a resizable map canvas with the normal console palette.</summary>
    public TacticalPlanningMapControl()
    {
        UseAvailableSize = true;
        SetSize = new Vector2(float.NaN, float.NaN);
        MinSize = new Vector2(400f, 400f);
        WallColor = Color.FromHex("#68DCEB");
        TileColor = Color.FromHex("#173540");
        BackgroundColor = Color.FromSrgb(TileColor.WithAlpha(BackgroundOpacity));
    }

    /// <summary>Recolors the cached map for the syndicate console variant.</summary>
    public void SetSyndicatePalette(bool syndicate)
    {
        if (_syndicatePalette == syndicate)
            return;

        _syndicatePalette = syndicate;
        WallColor = Color.FromHex(syndicate ? "#EF6B64" : "#68DCEB");
        TileColor = Color.FromHex(syndicate ? "#23343A" : "#173540");
        BackgroundColor = Color.FromSrgb(TileColor.WithAlpha(BackgroundOpacity));
        // Floor geometry caches its tint; rebuild it once when the palette changes.
        ForceNavMapUpdate();
    }

    /// <summary>Draws shared ink above the inherited department labels.</summary>
    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        // Base draws department labels last. Tactical ink must remain legible above them.
        DrawAnnotations(handle);
    }

    /// <summary>Replaces the annotations rendered over the map.</summary>
    public void SetAnnotations(List<TacticalPlanAnnotation> annotations)
    {
        _annotations.Clear();
        _annotations.AddRange(annotations);
    }

    /// <summary>Highlights one annotation when its details are inspected.</summary>
    public void SelectAnnotation(int? id)
        => _selectedId = id;

    /// <summary>Finds the topmost drawing close to a map position.</summary>
    public TacticalPlanAnnotation? FindAnnotation(Vector2 position)
    {
        var radius = 10f / MinimapScale;
        var maxDistanceSquared = radius * radius;
        var iconHitRadius = ((IconSize / 2f) + IconHitPadding) / MinimapScale;

        for (var index = _annotations.Count - 1; index >= 0; index--)
        {
            var annotation = _annotations[index];
            var points = annotation.Points;
            if (points.Count == 0)
                continue;

            if (annotation.Kind == TacticalPlanAnnotationKind.Dot)
            {
                if (Vector2.DistanceSquared(position, points[0]) <= maxDistanceSquared)
                    return annotation;
                continue;
            }

            if (annotation.Kind == TacticalPlanAnnotationKind.Icon)
            {
                // A padded square is much easier to select or erase than the sprite's opaque pixels.
                var delta = position - points[0];
                if (MathF.Abs(delta.X) <= iconHitRadius && MathF.Abs(delta.Y) <= iconHitRadius)
                    return annotation;
                continue;
            }

            for (var pointIndex = 1; pointIndex < points.Count; pointIndex++)
            {
                var a = points[pointIndex - 1];
                var b = points[pointIndex];
                var ab = b - a;
                var lengthSquared = ab.LengthSquared();
                var t = lengthSquared > 0f ? Math.Clamp(Vector2.Dot(position - a, ab) / lengthSquared, 0f, 1f) : 0f;
                var nearest = a + (t * ab);
                if (Vector2.DistanceSquared(position, nearest) <= maxDistanceSquared)
                    return annotation;
            }
        }

        return null;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
            _leftDownPosition = args.PointerLocation.Position;

        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            _rightHeld = true;
            _rightMoved = false;
            _rightDownPosition = args.PointerLocation.Position;
            _pendingStroke.Clear();
            _pointSpacing = InitialPointSpacing;
            if (CanEdit && Tool == TacticalPlanTool.Draw && TryGetMapPosition(args.PointerLocation.Position, out var position))
                _pendingStroke.Add(position);
            return;
        }

        base.KeyBindDown(args);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            if (Vector2.DistanceSquared(_leftDownPosition, args.PointerLocation.Position) < 25f
                && TryGetMapPosition(args.PointerLocation.Position, out var position))
                AnnotationInspected?.Invoke(position);
            return;
        }

        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            _rightHeld = false;
            if (!CanEdit)
            {
                _pendingStroke.Clear();
                return;
            }

            if (!TryGetMapPosition(args.PointerLocation.Position, out var position))
            {
                _pendingStroke.Clear();
                return;
            }

            switch (Tool)
            {
                case TacticalPlanTool.Draw:
                    if (!_rightMoved && _pendingStroke.Count > 0)
                        AnnotationAdded?.Invoke(TacticalPlanAnnotationKind.Dot, new List<Vector2> { position });
                    else
                    {
                        AddStrokePoint(position, true);
                        if (_pendingStroke.Count >= 2)
                            AnnotationAdded?.Invoke(TacticalPlanAnnotationKind.Stroke, new List<Vector2>(_pendingStroke));
                    }
                    _pendingStroke.Clear();
                    break;
                case TacticalPlanTool.Erase:
                    AnnotationErased?.Invoke(position);
                    break;
                case TacticalPlanTool.Icons:
                    IconAdded?.Invoke(position, SelectedIcon);
                    break;
            }
            return;
        }

        base.KeyBindUp(args);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (_rightHeld && Vector2.DistanceSquared(_rightDownPosition, args.RelativePixelPosition + GlobalPixelPosition) >= 25f)
            _rightMoved = true;
        if (_rightHeld && CanEdit && Tool == TacticalPlanTool.Draw
            && TryGetMapPosition(args.RelativePixelPosition + GlobalPixelPosition, out var position))
            AddStrokePoint(position);
    }

    private bool TryGetMapPosition(Vector2 pointerPosition, out Vector2 position)
    {
        position = default;
        if (MapUid == null || MinimapScale <= 0f)
            return false;

        var local = pointerPosition - GlobalPixelPosition;
        if (local.X < 0f || local.Y < 0f || local.X > PixelSize.X || local.Y > PixelSize.Y)
            return false;

        // Annotations use grid-local coordinates centered on the grid's physics bounds.
        // DrawAnnotations applies the matching center offset when converting back.
        position = InverseMapPosition(local);
        if (EntManager.TryGetComponent<PhysicsComponent>(MapUid, out var physics))
            position += physics.LocalCenter;
        return true;
    }

    private void AddStrokePoint(Vector2 position, bool force = false)
    {
        if (_pendingStroke.Count >= MaxStrokePoints)
        {
            // Retain every other sample and double the spacing, so a long drag keeps its
            // full shape without sending an ever-growing point list to the server.
            for (var i = 1; i < _pendingStroke.Count / 2; i++)
                _pendingStroke[i] = _pendingStroke[i * 2];
            _pendingStroke.RemoveRange(_pendingStroke.Count / 2, _pendingStroke.Count / 2);
            _pointSpacing *= 2f;
        }

        if (_pendingStroke.Count == 0
            || (force && _pendingStroke[^1] != position)
            || Vector2.DistanceSquared(_pendingStroke[^1], position) >= _pointSpacing * _pointSpacing)
            _pendingStroke.Add(position);
    }

    private void DrawAnnotations(DrawingHandleScreen handle)
    {
        if (MapUid == null)
            return;

        var offset = Offset;
        // This mirrors TryGetMapPosition: pan/zoom do not change stored annotation points.
        if (EntManager.TryGetComponent<PhysicsComponent>(MapUid, out var physics))
            offset += physics.LocalCenter;

        foreach (var annotation in _annotations)
        {
            var selected = annotation.Id == _selectedId;
            DrawAnnotation(handle, annotation.Points, annotation.Kind, annotation.Color, annotation.Icon, offset, selected);
        }

        if (_pendingStroke.Count > 1)
            DrawAnnotation(handle, _pendingStroke, TacticalPlanAnnotationKind.Stroke, InkColor, TacticalPlanIcon.None, offset, false);
    }

    private void DrawAnnotation(DrawingHandleScreen handle, List<Vector2> points,
        TacticalPlanAnnotationKind kind, Color color, TacticalPlanIcon icon, Vector2 offset, bool selected)
    {
        if (points.Count == 0)
            return;

        if (kind == TacticalPlanAnnotationKind.Icon)
        {
            var center = ToScreen(points[0], offset);
            var half = new Vector2(IconSize / 2f);
            var box = new UIBox2(center - half, center + half);
            if (selected)
            {
                handle.DrawLine(box.TopLeft, box.TopRight, Color.White);
                handle.DrawLine(box.TopRight, box.BottomRight, Color.White);
                handle.DrawLine(box.BottomRight, box.BottomLeft, Color.White);
                handle.DrawLine(box.BottomLeft, box.TopLeft, Color.White);
            }

            handle.DrawTextureRectRegion(TacticalPlanIconTextures.Get(icon), box,
                TacticalPlanIconTextures.GetMapRegion(icon));
            return;
        }

        if (kind == TacticalPlanAnnotationKind.Dot)
        {
            var center = ToScreen(points[0], offset);
            if (selected)
                handle.DrawCircle(center, 9f, Color.White);
            handle.DrawCircle(center, 6f, color);
            return;
        }

        for (var index = 1; index < points.Count; index++)
        {
            var start = ToScreen(points[index - 1], offset);
            var end = ToScreen(points[index], offset);
            var direction = end - start;
            var normal = direction.LengthSquared() > 0f
                ? Vector2.Normalize(new Vector2(-direction.Y, direction.X))
                : Vector2.Zero;

            if (selected)
            {
                handle.DrawLine(start + (normal * 2f), end + (normal * 2f), Color.White);
                handle.DrawLine(start - (normal * 2f), end - (normal * 2f), Color.White);
            }

            handle.DrawLine(start + normal, end + normal, color);
            handle.DrawLine(start, end, color);
            handle.DrawLine(start - normal, end - normal, color);
        }
    }

    private Vector2 ToScreen(Vector2 position, Vector2 offset)
    {
        var relative = position - offset;
        return ScalePosition(new Vector2(relative.X, -relative.Y));
    }
}

public enum TacticalPlanTool : byte
{
    Draw,
    Erase,
    Icons,
}
