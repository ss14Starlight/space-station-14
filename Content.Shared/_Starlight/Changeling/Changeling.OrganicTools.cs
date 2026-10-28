using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Changeling;

public sealed partial class ChangelingEmagEvent : EntityTargetActionEvent
{
    // Paid locally after the native access-breaker API reports success.
    [DataField] public float ChemicalCost = 30;
}

public sealed partial class ToggleChangelingShockHandEvent : InstantActionEvent;

[RegisterComponent, NetworkedComponent]
public sealed partial class ChangelingShockHandComponent : Component
{
    [DataField] public float ChemicalCost = 10;

    public EntityUid? User;

    public EntityUid? Action;
}

public sealed partial class ToggleChangelingBreathingEvent : InstantActionEvent;
