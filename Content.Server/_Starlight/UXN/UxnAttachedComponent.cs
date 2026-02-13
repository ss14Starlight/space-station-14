using Robust.Shared.Containers;

namespace Content.Server._Starlight.UXN;

[RegisterComponent]
public sealed partial class UxnAttachedComponent : Component
{
    [ViewVariables]
    public UXNProcessor? Uxn = null;

    [ViewVariables]
    public ContainerSlot ChipHolder = new();
}