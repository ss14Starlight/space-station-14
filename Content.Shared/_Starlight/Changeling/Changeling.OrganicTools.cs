using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Changeling;

/// <summary>
/// Requests the organic access-breaker effect on a target. The existing event name is retained by action prototypes.
/// </summary>
public sealed partial class ChangelingEmagEvent : EntityTargetActionEvent;

/// <summary>
/// Requests growing or retracting the changeling's electrified hand.
/// </summary>
public sealed partial class ToggleChangelingShockHandEvent : InstantActionEvent;

/// <summary>
/// Connects a grown shock hand to the changeling that powers it and the action whose toggle must follow its lifetime.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ChangelingShockHandComponent : Component
{
    /// <summary>
    /// Chemicals charged once per eligible melee-hit event with at least one target.
    /// </summary>
    [DataField] public float ChemicalCost = 10;

    /// <summary>
    /// Original owner, who must still hold the hand and meet its resource requirements to use the stamina effect.
    /// </summary>
    public EntityUid? User;

    /// <summary>
    /// Action to untoggle when the hand is removed, including removal outside the normal retract action.
    /// </summary>
    public EntityUid? Action;
}
