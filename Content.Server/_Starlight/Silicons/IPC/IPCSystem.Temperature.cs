// IPC System - Temperature (Server)
// _STARLIGHT: Original implementation
// Temperature-based effects for IPCs:
// - Overheating causes emergency shutdown (knockdown) to prevent death
// - Alarm sounds play on overheat shutdown

using Content.Shared._Starlight.Silicons.IPC.Components;
using Content.Shared.Temperature;
using Robust.Shared.Audio;

namespace Content.Server._Starlight.Silicons.IPC;

public sealed partial class IPCSystem
{
    // IPCs shut down at 335K (~62°C) to prevent heat death
    private const float OverheatThreshold = 335f;
    // Emergency shutdown lasts 8 seconds
    private const float OverheatKnockdownDuration = 8f;

    private void InitializeTemperature()
    {
        SubscribeLocalEvent<IPCBatteryComponent, OnTemperatureChangeEvent>(OnIPCTemperatureChange);
    }

    /// <summary>
    /// Handles IPC temperature changes and triggers emergency shutdown on overheat
    /// </summary>
    private void OnIPCTemperatureChange(EntityUid uid, IPCBatteryComponent component, OnTemperatureChangeEvent args)
    {
        // If temperature exceeds overheat threshold, initiate emergency shutdown
        if (args.CurrentTemperature >= OverheatThreshold)
        {
            // Check if we should apply knockdown (don't spam it)
            var currentTime = _timing.CurTime;
            if (component.LastOverheatKnockdown == null || 
                currentTime - component.LastOverheatKnockdown > TimeSpan.FromSeconds(OverheatKnockdownDuration + 1))
            {
                // Play overheat alarm sound
                _audio.PlayEntity(new SoundPathSpecifier("/Audio/Weapons/Guns/EmptyAlarm/smg_empty_alarm.ogg"), uid, uid);
                
                // Apply knockdown (emergency shutdown)
                _stun.TryKnockdown(uid, TimeSpan.FromSeconds(OverheatKnockdownDuration), autoStand: true);
                component.LastOverheatKnockdown = currentTime;
            }
        }
    }
}
