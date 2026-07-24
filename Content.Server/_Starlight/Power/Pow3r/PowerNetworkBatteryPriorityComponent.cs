using Content.Server.Power.Components;

namespace Content.Server.Power.Components
{
    /// <summary>
    ///     Optional companion to <see cref="PowerNetworkBatteryComponent"/>.
    ///     Batteries with a higher <see cref="Priority"/> value will only be
    ///     allowed to discharge once all batteries sharing the same output
    ///     network with a lower Priority are already close to their supply
    ///     cap (i.e. can't meet demand on their own).
    /// </summary>
    /// <remarks>
    ///     Entities without this component are treated as Priority 0
    ///     ("always eligible" / primary tier) by
    ///     <see cref="PowerNetworkBatteryPrioritySystem"/>.
    ///     This does not modify the pow3r solver at all - it only toggles
    ///     <see cref="PowerNetworkBatteryComponent.CanDischarge"/> on/off
    ///     from the outside based on how saturated lower-priority tiers are.
    /// </remarks>
    [RegisterComponent]
    public sealed partial class PowerNetworkBatteryPriorityComponent : Component
    {
        /// <summary>
        ///     Lower values discharge first. Must be greater than 0 to have
        ///     any effect (0 is the implicit "always eligible" tier).
        /// </summary>
        [DataField("priority")]
        public int Priority = 1;

        /// <summary>
        ///     Fraction (0-1) of the combined MaxSupply that lower-priority
        ///     tiers must reach/exceed before this battery is allowed to
        ///     start discharging.
        /// </summary>
        [DataField("engageThreshold")]
        public float EngageThreshold = 0.98f;

        /// <summary>
        ///     Fraction (0-1) of the combined MaxSupply that lower-priority
        ///     tiers must drop back below before this battery disengages
        ///     again. Should be lower than EngageThreshold to give hysteresis
        ///     and avoid rapid on/off flapping.
        /// </summary>
        [DataField("disengageThreshold")]
        public float DisengageThreshold = 0.90f;

        /// <summary>
        ///     Fraction (0-1) of combined stored charge (CurrentStorage /
        ///     Capacity) that lower-priority tiers must drop AT OR BELOW
        ///     before this battery engages, regardless of rate saturation.
        ///     Catches a primary that is running dry even though it is not
        ///     currently rate-capped.
        /// </summary>
        [DataField("chargeEngageThreshold")]
        public float ChargeEngageThreshold = 0.20f;

        /// <summary>
        ///     Fraction (0-1) of combined stored charge that lower-priority
        ///     tiers must climb back above before this battery is allowed to
        ///     disengage on the charge signal. Should be higher than
        ///     ChargeEngageThreshold to give hysteresis.
        /// </summary>
        [DataField("chargeDisengageThreshold")]
        public float ChargeDisengageThreshold = 0.40f;

        /// <summary>
        ///     Whether this battery is currently permitted to discharge.
        ///     Runtime-only, not saved/loaded.
        /// </summary>
        [ViewVariables]
        public bool Engaged;
    }
}
