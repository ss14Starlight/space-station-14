using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Actions.EntitySystems;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(DamageableSystem), typeof(SharedJumpSystem))]
public sealed partial class JumpPenaltyComponent : Component
{
    /// <summary>
    /// Whether jumping is disabled entirely or not.
    /// </summary>
    [DataField, AutoNetworkedField] public bool JumpDisabled;

    /// <summary>
    /// Remaining time that jumping is disabled for.
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan JumpDisabledTimer;

    /// <summary>
    /// How much time in seconds to disable jumping for when hitting the damage over time threshold.
    /// </summary>
    [DataField, AutoNetworkedField] public TimeSpan DamageOverTimePenalty;
    
    /// <summary>
    /// How much damage over time is required to disable jumping.
    /// </summary>
    [DataField, AutoNetworkedField] public (float, TimeSpan) DamageOverTimeThreshold;

    /// <summary>
    /// How much total damage is required to trigger distance penalty.
    /// </summary>
    [DataField, AutoNetworkedField] public Dictionary<FixedPoint2, float> HighDamageThresholds;
}