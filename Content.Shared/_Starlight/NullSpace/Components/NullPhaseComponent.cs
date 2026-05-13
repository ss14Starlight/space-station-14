using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.NullSpace;

[RegisterComponent]
public sealed partial class NullPhaseComponent : Component
{
    [DataField]
    public EntityUid? PhaseAction;

    [DataField] public bool PreventLightFlicker;
    public bool OriginalFlickerFlagState;
}

public sealed partial class NullPhaseActionEvent : InstantActionEvent { }
