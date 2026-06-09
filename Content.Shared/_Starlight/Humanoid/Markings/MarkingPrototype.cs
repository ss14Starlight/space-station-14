using System.Numerics;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

// ReSharper disable CheckNamespace

namespace Content.Shared.Humanoid.Markings;

public sealed partial class MarkingPrototype : IInheritingPrototype
{
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<MarkingPrototype>))]
    public string[]? Parents { get; private set; }

    [AbstractDataField]
    [NeverPushInheritance]
    public bool Abstract { get; private set; }

    [DataField]
    public string? WaggingId;

    [DataField]
    public string? StaticId;

    /// <summary>
    /// Optional visual layer anchors for each sprite in <see cref="MarkingPrototype.Sprites"/>.
    /// Body-part anchors insert above the body part; other anchors insert below the anchor layer.
    /// </summary>
    [DataField]
    public List<HumanoidVisualLayers>? SpriteLayers;

    /// <summary>
    /// Optional color slot indexes for each sprite in <see cref="MarkingPrototype.Sprites"/>.
    /// Multiple sprites can share one index to reuse a single color picker.
    /// </summary>
    [DataField]
    public List<int>? SpriteColorIndexes;

    public int ColorSlotCount
    {
        get
        {
            if (SpriteColorIndexes is not { Count: > 0 })
                return Sprites.Count;

            var count = 0;
            for (var i = 0; i < Sprites.Count; i++)
            {
                var colorIndex = GetColorIndex(i);
                if (colorIndex >= count)
                    count = colorIndex + 1;
            }

            return count;
        }
    }

    public int GetColorIndex(int spriteIndex)
    {
        if (SpriteColorIndexes is not { Count: > 0 } || spriteIndex >= SpriteColorIndexes.Count)
            return spriteIndex;

        return Math.Max(0, SpriteColorIndexes[spriteIndex]);
    }

    public List<Color> GetColorSlotColors(IReadOnlyList<Color> colors)
    {
        var colorSlotCount = ColorSlotCount;
        var result = new List<Color>(colorSlotCount);
        for (var i = 0; i < colorSlotCount; i++)
        {
            result.Add(Color.White);
        }

        if (colors.Count == colorSlotCount)
        {
            for (var i = 0; i < colorSlotCount; i++)
            {
                result[i] = colors[i];
            }

            return result;
        }

        if (SpriteColorIndexes is { Count: > 0 } && colors.Count == Sprites.Count)
        {
            var assignedSlots = new bool[colorSlotCount];
            for (var i = 0; i < Sprites.Count; i++)
            {
                var colorIndex = GetColorIndex(i);
                if (colorIndex >= colorSlotCount || assignedSlots[colorIndex])
                    continue;

                result[colorIndex] = colors[i];
                assignedSlots[colorIndex] = true;
            }

            return result;
        }

        for (var i = 0; i < colors.Count && i < colorSlotCount; i++)
        {
            result[i] = colors[i];
        }

        return result;
    }
}
