// Temperature Shutdown System
// _STARLIGHT: Original implementation for Starlight
//
// How it works:
// 1. Every frame, query all entities with temperature shutdown component
// 2. Check if entity is alive and temperature exceeds heat threshold
// 3. If true: display popup message and change mob state
//
// Design rationale:
// - Uses Update() rather than event-based because temperature checks are continuous
// - Skips dead/critical entities for performance
// - Shows popup BEFORE state change for player feedback
// - Changes mob state based on component configuration (default: Critical)
// - Cold only slows actions (via IPCColdSlowedComponent), does not cause shutdown

using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Temperature.Components;

namespace Content.Server._Starlight.Temperature;

/// <summary>
/// Handles emergency shutdown for entities that exceed temperature thresholds.
/// Queries entities every frame and changes their mob state when temperature is too high.
/// </summary>
/// <remarks>
/// This system is intentionally simple and direct. Alternative approaches considered:
/// - Event-based on temperature change: Too many events, performance concern
/// - Interval-based checking: Could miss rapid temperature spikes
/// - Current approach (frame-based): Simple, reliable, minimal overhead
/// </remarks>
public sealed class KillOnOverheatSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mob = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    /// <summary>
    /// Checks all entities with temperature shutdown components each frame.
    /// Changes mob state for any that exceed their heat threshold.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Query all entities that can be shut down by temperature
        var query = EntityQueryEnumerator<KillOnOverheatComponent, TemperatureComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out var temp, out var mob))
        {
            // Check if temperature exceeds overheat threshold
            if (temp.CurrentTemperature > comp.OverheatThreshold)
            {
                // Performance optimization: only affect alive entities
                if (mob.CurrentState != MobState.Alive)
                    continue;

                // Entity has overheated! Show popup and change mob state
                var msg = Loc.GetString(comp.OverheatPopup, ("name", Identity.Name(uid, EntityManager)));
                _popup.PopupEntity(msg, uid, PopupType.LargeCaution);
                
                // Change to configured mob state (default: Critical, allowing recovery)
                _mob.ChangeMobState(uid, comp.TargetMobState, mob);
            }
        }
    }
}
