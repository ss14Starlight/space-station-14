using Robust.Shared.Utility;

namespace Content.Server._Starlight.UXN;

[RegisterComponent]
public sealed partial class UxnAttachableComponent : Component
{
    [DataField]
    public ResPath UxntalSourceFile = new("/_Starlight/Uxn/Tal/opctest.tal");
    public UXNProcessor? Uxn = null;
}