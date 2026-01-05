using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.NullSpace;

[RegisterComponent]
public sealed partial class NullPhaseComponent : Component
{
    [DataField]
    public EntityUid? PhaseAction;
}

public sealed partial class NullPhaseActionEvent : InstantActionEvent { }