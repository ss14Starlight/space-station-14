// Temperature Shutdown Component
// _STARLIGHT: Original implementation for Starlight
//
// Purpose: Knock out IPCs when temperature reaches extreme heat threshold,
// simulating protective circuit shutdown to prevent permanent damage.
// Cold temperatures only slow actions (via IPCColdSlowedComponent).

using Content.Shared.Atmos;
using Content.Shared.Mobs;

namespace Content.Server._Starlight.Temperature;

/// <summary>
/// Component that causes an entity to be knocked out when temperature exceeds heat threshold.
/// Originally designed for IPC species to simulate emergency shutdown systems
/// that protect circuits from permanent damage in extreme heat.
/// </summary>
/// <remarks>
/// This provides instant knockout at temperature threshold rather than gradual damage,
/// representing emergency protective systems in robotic entities.
/// Cold only slows actions, does not cause shutdown.
/// </remarks>
[RegisterComponent, Access(typeof(KillOnOverheatSystem))]
public sealed partial class KillOnOverheatComponent : Component
{
    /// <summary>
    /// Temperature threshold (in Kelvin) at which the entity shuts down from overheating.
    /// Default: 403.2K (130°C) - 30% above normal operating temperature.
    /// </summary>
    [DataField]
    public float OverheatThreshold = Atmospherics.T0C + 130f;

    /// <summary>
    /// Localization key for the popup message displayed when entity overheats.
    /// </summary>
    [DataField]
    public LocId OverheatPopup = "ipc-overheat-popup";

    /// <summary>
    /// The mob state to set the entity to when overheating.
    /// Default: Critical (allows recovery when temperature drops)
    /// </summary>
    [DataField]
    public MobState TargetMobState = MobState.Critical;
}
