using Robust.Shared.Utility;

namespace Content.Shared._Starlight.UXN;

[RegisterComponent]
public sealed partial class UxnAttachedComponent : Component
{
    [DataField]
    public ResPath UxntalSourceFile = new("/_Starlight/Uxn/Tal/opctest.tal");
    public UXNProcessor? Uxn = null;
}