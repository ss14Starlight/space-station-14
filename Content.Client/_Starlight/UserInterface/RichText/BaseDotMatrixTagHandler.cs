using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.UserInterface.RichText;

public abstract partial class BaseDotMatrixTagHandler : IMarkupTagHandler
{
    /// <summary>
    /// Four-bit lookup table for font variants. See <see cref="FontVariation"/>.
    /// </summary>
    private static readonly ProtoId<FontPrototype>[] _fontVariants =
    [
        "DotMatrix",
        "DotMatrixItalic",
        "DotMatrixBold",
        "DotMatrixBoldItalic",
        "DotMatrixCondensed",
        "DotMatrixCondensedItalic",
        "DotMatrixCondensedBold",
        "DotMatrixCondensedBoldItalic",

        "DotMatrixDuo",
        "DotMatrixDuoItalic",
        "DotMatrixDuoBold",
        "DotMatrixDuoBoldItalic",
        "DotMatrixDuoCondensed",
        "DotMatrixDuoCondensedItalic",
        "DotMatrixDuoCondensedBold",
        "DotMatrixDuoCondensedBoldItalic",
    ];

    /// <summary>
    /// Enum with bit masks for indexing the above font variation array.
    /// </summary>
    private enum FontVariation
    {
        Duo = 8,
        Condensed = 4,
        Bold = 2,
        Italic = 1,
    }

    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public abstract string Name { get; }

    /// <summary>
    /// Check the node for a specific boolean parameter. Two strings, enableName and disableName, toggle the flag on or
    /// off respectively. The default value can also be provided.
    /// </summary>
    /// <param name="node">The node to extract a flag from</param>
    /// <param name="enableName">If an attribute with this name is present, enables the flag</param>
    /// <param name="disableName">If an attribute with this name is present, disables the flag</param>
    /// <param name="defaultValue">The default value if neither enable nor disable attributes are present</param>
    /// <returns>The flag value</returns>
    private static bool GetBoolParam(MarkupNode node, string enableName, string? disableName = null,
        bool defaultValue = false)
    {
        if (disableName != null && node.Attributes.ContainsKey(disableName))
            return false;

        return node.Attributes.ContainsKey(enableName) || defaultValue;
    }

    protected void PushDrawContextInternal(MarkupNode node, MarkupDrawingContext context, bool duoDefault = false,
        bool boldDefault = false, bool condensedDefault = false, bool italicDefault = false)
    {
        var duo = GetBoolParam(node, "duo", "single", duoDefault);
        var condensed = GetBoolParam(node, "condensed", "expanded", condensedDefault);
        var bold = GetBoolParam(node, "bold", "regular", boldDefault);
        var italic = GetBoolParam(node, "italic", "regular", italicDefault);

        var index =
            (duo ? (int)FontVariation.Duo : 0) |
            (condensed ? (int)FontVariation.Condensed : 0) |
            (bold ? (int)FontVariation.Bold : 0) |
            (italic ? (int)FontVariation.Italic : 0);

        var font = FontTag.CreateFont(context.Font, node, _resourceCache, _prototypeManager, _fontVariants[index]);
        context.Font.Push(font);
    }

    /// <inheritdoc/>
    public abstract void PushDrawContext(MarkupNode node, MarkupDrawingContext context);

    /// <inheritdoc/>
    public void PopDrawContext(MarkupNode node, MarkupDrawingContext context) => context.Font.Pop();
}
