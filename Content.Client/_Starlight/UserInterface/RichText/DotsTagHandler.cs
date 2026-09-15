using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.UserInterface.RichText;

public sealed partial class DotsTagHandler : BaseDotMatrixTagHandler
{
    public override string Name => "dots";

    public override void PushDrawContext(MarkupNode node, MarkupDrawingContext context) =>
        PushDrawContextInternal(node, context);
}
