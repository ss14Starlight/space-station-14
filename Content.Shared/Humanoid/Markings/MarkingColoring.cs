using Robust.Shared.Utility;

namespace Content.Shared.Humanoid.Markings;

/// <summary>
///     Default colors for marking
/// </summary>
[DataDefinition]
public sealed partial class MarkingColors
{
    /// <summary>
    /// Coloring properties that will be used on any unspecified layer
    /// </summary>
    [DataField("default", true)]
    public LayerColoringDefinition Default = new LayerColoringDefinition();

    /// <summary>
    ///     Layers with their own coloring type and properties
    /// </summary>
    [DataField("layers", true)]
    public Dictionary<string, LayerColoringDefinition>? Layers;
}

public static class MarkingColoring
{
    /// <summary>
    ///     Returns list of colors for marking layers
    /// </summary>
    public static List<Color> GetMarkingLayerColors
    (
        MarkingPrototype prototype,
        Color? skinColor,
        Color? eyeColor,
        MarkingSet markingSet
    )
    {
        var colors = new List<Color>();

        // Coloring from default properties
        var defaultColor = prototype.Coloring.Default.GetColor(skinColor, eyeColor, markingSet);

        // Starlight - markings can expose fewer color slots than sprite layers.
        var colorSlotCount = prototype.ColorSlotCount;

        if (prototype.Coloring.Layers == null)
        {
            // If layers is not specified, then every layer must be default
            for (var i = 0; i < colorSlotCount; i++)
            {
                colors.Add(defaultColor);
            }
            return colors;
        }
        else
        {
            for (var i = 0; i < colorSlotCount; i++)
            {
                colors.Add(defaultColor);
            }

            var coloredSlots = new bool[colorSlotCount];

            // If some layers are specified.
            for (var i = 0; i < prototype.Sprites.Count; i++)
            {
                // Starlight start - multiple sprite layers can share one color slot.
                var colorIndex = prototype.GetColorIndex(i);
                if (colorIndex >= colorSlotCount || coloredSlots[colorIndex])
                    continue;
                // Starlight end

                // Getting layer name
                string? name = prototype.Sprites[i] switch
                {
                    SpriteSpecifier.Rsi rsi => rsi.RsiState,
                    SpriteSpecifier.Texture texture => texture.TexturePath.Filename,
                    _ => null
                };
                if (name == null)
                    continue;

                // All specified layers must be colored separately, all unspecified must depend on default coloring
                if (prototype.Coloring.Layers.TryGetValue(name, out var layerColoring))
                {
                    var marking_color = layerColoring.GetColor(skinColor, eyeColor, markingSet);
                    // Starlight edit - assign layer coloring to its shared color slot.
                    colors[colorIndex] = marking_color;
                    coloredSlots[colorIndex] = true;
                }
            }
            return colors;
        }
    }
}

/// <summary>
///     A class that defines coloring type and fallback for markings
/// </summary>
[DataDefinition]
public sealed partial class LayerColoringDefinition
{
    [DataField("type")]
    public LayerColoringType? Type = new SkinColoring();

    /// <summary>
    ///     Coloring types that will be used if main coloring type will return nil
    /// </summary>
    [DataField("fallbackTypes")]
    public List<LayerColoringType> FallbackTypes = new() {};

    /// <summary>
    ///     Color that will be used if coloring type and fallback type will return nil
    /// </summary>
    [DataField("fallbackColor")]
    public Color FallbackColor = Color.White;

    public Color GetColor(Color? skin, Color? eyes, MarkingSet markingSet)
    {
        Color? color = null;
        if (Type != null)
            color = Type.GetColor(skin, eyes, markingSet);
        if (color == null)
        {
            foreach (var type in FallbackTypes)
            {
                color = type.GetColor(skin, eyes, markingSet);
                if (color != null) break;
            }
        }
        return color ?? FallbackColor;
    }
}

/// <summary>
///     An abstract class for coloring types
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class LayerColoringType
{
    /// <summary>
    ///     Makes output color negative
    /// </summary>
    [DataField("negative")]
    public bool Negative { get; private set; } = false;
    public abstract Color? GetCleanColor(Color? skin, Color? eyes, MarkingSet markingSet);
    public Color? GetColor(Color? skin, Color? eyes, MarkingSet markingSet)
    {
        var color = GetCleanColor(skin, eyes, markingSet);
        // Negative color
        if (color != null && Negative)
        {
            var rcolor = color.Value;
            rcolor.R = 1f-rcolor.R;
            rcolor.G = 1f-rcolor.G;
            rcolor.B = 1f-rcolor.B;
            return rcolor;
        }
        return color;
    }
}
