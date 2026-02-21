using Robust.Shared.Containers;

namespace Content.Server._Starlight.UXN;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class UxnAttachedComponent : Component
{
    [ViewVariables]
    public UXNProcessor? Uxn = null;

    [ViewVariables]
    public ContainerSlot ChipHolder = new();
    
    /// <summary>
    /// when set this UXN's execution is skipped until the specified time frame. this also makes it stop counting towards the global instruction limit.
    /// </summary>

    [ViewVariables, AutoPausedField]
    public TimeSpan? DelayExecution = null;
}