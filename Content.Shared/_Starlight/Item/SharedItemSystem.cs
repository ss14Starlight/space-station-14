using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;

namespace Content.Shared.Item;

public abstract partial class SharedItemSystem
{
    /// <summary>
    /// Gets an arbitrary item shape, adjusting for rotation and offset.
    /// </summary>
    public void GetAdjustedItemShape(List<Box2i> adjustedShapes, IReadOnlyList<Box2i> shapes, Angle rotation, Vector2i position)
    {
        if (shapes.Count == 0)
            return;

        var boundingShape = shapes.GetBoundingBox();
        var boundingCenter = ((Box2) boundingShape).Center;
        var matty = Matrix3Helpers.CreateTransform(boundingCenter, rotation);
        var drift = boundingShape.BottomLeft - matty.TransformBox(boundingShape).BottomLeft;

        foreach (var shape in shapes)
        {
            var transformed = matty.TransformBox(shape).Translated(drift);
            var floored = new Box2i(transformed.BottomLeft.Floored(), transformed.TopRight.Floored());
            var translated = floored.Translated(position);

            adjustedShapes.Add(translated);
        }
    }

    /// <summary>
    /// Gets the size and shape the item would have after toggle.
    /// </summary>
    public bool TryGetToggledShape(
        Entity<ItemToggleSizeComponent, ItemComponent?> ent,
        bool activated,
        out ProtoId<ItemSizePrototype> size,
        out IReadOnlyList<Box2i> shape)
    {
        size = default;
        shape = [];

        if (!Resolve(ent, ref ent.Comp2, false))
            return false;

        var toggle = ent.Comp1;
        var item = ent.Comp2;

        var newSize = activated ? toggle.ActivatedSize : toggle.DeactivatedSize;
        var newShape = activated ? toggle.ActivatedShape : toggle.DeactivatedShape;

        size = newSize ?? item.Size;
        shape = newShape ?? item.Shape ?? GetSizePrototype(size).DefaultShape;
        return true;
    }
}
