using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Zones;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class ZoneShapeSet
{
    [DataField(required: true)]
    public ProtoId<ZonePrototype> Zone;

    [DataField]
    public List<Box2i> Rects = new();

    [DataField]
    public List<ZoneCircle> Circles = new();

    public bool IsEmpty => Rects.Count == 0 && Circles.Count == 0;
}

[DataDefinition]
[Serializable, NetSerializable]
public partial struct ZoneCircle
{
    [DataField(required: true)]
    public Vector2 Center;

    [DataField(required: true)]
    public float Radius;

    public readonly Box2i Bounds()
    {
        var left = (int) MathF.Floor(Center.X - Radius);
        var bottom = (int) MathF.Floor(Center.Y - Radius);
        var right = (int) MathF.Ceiling(Center.X + Radius);
        var top = (int) MathF.Ceiling(Center.Y + Radius);
        return new Box2i(left, bottom, right, top);
    }

    public readonly bool ContainsTile(int x, int y)
    {
        var dx = x + 0.5f - Center.X;
        var dy = y + 0.5f - Center.Y;
        return dx * dx + dy * dy <= Radius * Radius;
    }
}
