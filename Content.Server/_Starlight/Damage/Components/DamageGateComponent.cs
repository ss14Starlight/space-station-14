using Content.Shared.FixedPoint;

namespace Content.Server._Starlight.Damage.Components;

[RegisterComponent]
public sealed partial class DamageGateComponent : Component
{
    /// <summary>
    /// Maximum total positive damage that can be applied by one damage call. Null disables this limit.
    /// </summary>
    [DataField] public FixedPoint2? MaxDamagePerHit;

    /// <summary>
    /// Minimum damage from one hit required to reach a breakage threshold.
    /// </summary>
    [DataField] public FixedPoint2? MinDamageToStage;

    /// <summary>
    /// Minimum damage from one hit required to reach a destruction threshold.
    /// </summary>
    [DataField] public FixedPoint2? MinDamageToDestruction;

    /// <summary>
    /// Limits a damage call to the next DamageTrigger threshold on the entity's DestructibleComponent.
    /// </summary>
    [DataField] public bool LimitToNextStage;

    /// <summary>
    /// Minimum time between positive damage calls. Healing is not blocked by this cooldown.
    /// </summary>
    [DataField] public TimeSpan DamageCooldown = TimeSpan.Zero;

    /// <summary>
    /// Time after crossing the next DamageTrigger threshold during which all positive damage is blocked.
    /// </summary>
    [DataField] public TimeSpan StageDamageCooldown = TimeSpan.Zero;

    [ViewVariables] public TimeSpan? DamageCooldownUntil;
    [ViewVariables] public TimeSpan? StageDamageCooldownUntil;
}
