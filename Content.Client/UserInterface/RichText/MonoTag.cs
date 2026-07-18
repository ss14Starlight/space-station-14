using Content.Client.Resources;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.RichText;

/// <summary>
/// Sets the font to a monospaced variant
/// </summary>
public sealed partial class MonoTag : IMarkupTagHandler
{
    private static readonly ResPath MonoFontPath = new("/EngineFonts/NotoSans/NotoSansMono-Regular.ttf");

    [Dependency] private IResourceCache _resourceCache = default!;

    public string Name => "mono";

    /// <inheritdoc/>
    public void PushDrawContext(MarkupNode node, MarkupDrawingContext context)
    {
        var size = FontTag.GetSizeForFontTag(context.Font, node);
        var font = _resourceCache.GetFont(MonoFontPath, size);
        context.Font.Push(font);
    }

    /// <inheritdoc/>
    public void PopDrawContext(MarkupNode node, MarkupDrawingContext context)
    {
        context.Font.Pop();
    }
}
