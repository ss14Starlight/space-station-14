using Robust.Shared.Utility;

namespace Content.Server._Starlight.UXN;

[RegisterComponent]
public sealed partial class UxnAttachableComponent : Component
{
    public UXNProcessor? Uxn = null;

    [ViewVariables]
    public Dictionary<string, UXNDevice> Devices = new();
}