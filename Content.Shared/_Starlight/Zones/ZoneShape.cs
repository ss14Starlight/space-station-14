using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Zones;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class ZoneShapeSet
{
    /// <summary>
    /// Current zone prototype which this shape set belongs to.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ZonePrototype> Zone;

    /// <summary>
    /// Rectangles which will be used to mark zone on grid, each rectangle is a box of tiles.
    /// </summary>
    [DataField]
    public List<Box2i> Rects = new();

    /// <summary>
    /// Circles which will be used to mark zone on grid, each circle is a circle of tiles.
    /// </summary>
    [DataField]
    public List<ZoneCircle> Circles = new();

    public bool IsEmpty => Rects.Count == 0 && Circles.Count == 0;
}

[DataDefinition]
[Serializable, NetSerializable]
public partial struct ZoneCircle
{
    /// <summary>
    /// Center of circle.
    /// </summary>
    [DataField(required: true)]
    public Vector2 Center;

    private float _radius;

    /// <summary>
    /// Radius of circle.
    /// </summary>
    [DataField(required: true)]
    public float Radius
    {
        readonly get => _radius;
        set => _radius = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Radius must be non-negative.");
    }

    /// <summary>
    /// Calculates the bounding box of the circle, returning a Box2i that encompasses the entire circle.
    /// </summary>
    public readonly Box2i Bounds()
    {
        var left = (int) MathF.Floor(Center.X - Radius);
        var bottom = (int) MathF.Floor(Center.Y - Radius);
        var right = (int) MathF.Ceiling(Center.X + Radius);
        var top = (int) MathF.Ceiling(Center.Y + Radius);
        return new Box2i(left, bottom, right, top);
    }

    /// <summary>
    /// Determines whether a given tile (x, y) is contained within the circle.
    /// </summary>
    public readonly bool ContainsTile(int x, int y)
    {
        var dx = x + 0.5f - Center.X;
        var dy = y + 0.5f - Center.Y;
        return dx * dx + dy * dy <= Radius * Radius;
    }
}
