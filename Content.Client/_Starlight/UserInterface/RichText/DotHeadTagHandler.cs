using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.UserInterface.RichText;

/// <summary>
/// Combination of the [dots] and [head] tags. Unfortunately, normal [head] tags replace the font on the font stack.
/// This provides a more convenient alternative for players, such as [dots duo condensed size=24] being equivalent to [dothead=1]
/// </summary>
public sealed partial class DotHeadTagHandler : BaseDotMatrixTagHandler
{
    public override string Name => "dothead";

    /// <inheritdoc/>
    public override void PushDrawContext(MarkupNode node, MarkupDrawingContext context)
    {
        if (!node.Value.TryGetLong(out var levelParam))
            levelParam = 1;

        var level = Math.Min(Math.Max((int)levelParam, 1), 3);
        node.Attributes["size"] = new MarkupParameter(
            (int)Math.Ceiling(FontTag.DefaultSize * 2 / Math.Sqrt(level))
        );

        PushDrawContextInternal(node, context, duoDefault: true, condensedDefault: true);
    }
}
