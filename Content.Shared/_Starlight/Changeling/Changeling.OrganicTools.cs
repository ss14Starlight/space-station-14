using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Changeling;

public sealed partial class ChangelingEmagEvent : EntityTargetActionEvent;
public sealed partial class ToggleChangelingShockHandEvent : InstantActionEvent;

[RegisterComponent, NetworkedComponent]
public sealed partial class ChangelingShockHandComponent : Component
{
    [DataField] public float ChemicalCost = 10;

    public EntityUid? User;
    public EntityUid? Action;
}
