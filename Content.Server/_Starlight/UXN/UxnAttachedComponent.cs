using Robust.Shared.Containers;

namespace Content.Server._Starlight.UXN;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class UxnAttachedComponent : Component
{
    [ViewVariables]
    public UXNProcessor? Uxn = null;

    [ViewVariables]
    public ContainerSlot ChipHolder = new();

    [ViewVariables, AutoPausedField]
    public TimeSpan? DelayExecution = null;
}