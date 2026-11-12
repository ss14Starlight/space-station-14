using Content.Shared.Chemistry;

namespace Content.Client._Starlight.Plumbing.Components;

[RegisterComponent]
public sealed partial class PlumbingSmartDispenserLocalStateComponent : Component
{
    [ViewVariables]
    public EntityUid? OutputContainerEntity;

    [ViewVariables]
    public ReagentDispenserDispenseAmount SelectedDispenseAmount = ReagentDispenserDispenseAmount.U5;
}
