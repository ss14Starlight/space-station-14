using Content.Shared.Actions;

namespace Content.Shared._Starlight.NullSpace.Components;

[RegisterComponent]
public sealed partial class NullPhaseComponent : Component
{
    [DataField]
    public EntityUid? PhaseAction;

    [DataField] public bool PreventLightFlicker;
    public bool OriginalFlickerFlagState;
}

public sealed partial class NullPhaseActionEvent : InstantActionEvent { }
