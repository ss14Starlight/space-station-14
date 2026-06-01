using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.RichText;

public sealed partial class DotsTag : BaseDotMatrixTag
{
    public override string Name => "dots";

    public override void PushDrawContext(MarkupNode node, MarkupDrawingContext context) =>
        PushDrawContextInternal(node, context);
}
