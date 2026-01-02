// IPC System - Temperature (Server)
// _STARLIGHT: Original implementation
// Temperature-based effects for IPCs:
// - Overheating causes emergency shutdown (knockdown) to prevent death
// - Alarm sounds play on overheat shutdown

using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Temperature;
using Robust.Shared.Audio;

namespace Content.Server._FarHorizons.Silicons.IPC;

public sealed partial class IPCSystem
{
    // IPCs shut down at 360K (~87°C) to prevent heat death (below 373K damage threshold)
    private const float OverheatThreshold = 360f;
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
            // Only trigger shutdown if not already critical/unconscious
            if (TryComp<MobStateComponent>(uid, out var mobState) && 
                mobState.CurrentState != MobState.Critical && 
                mobState.CurrentState != MobState.Dead)
            {
                // Play overheat alarm sound
                _audio.PlayEntity(new SoundPathSpecifier("/Audio/Weapons/Guns/EmptyAlarm/smg_empty_alarm.ogg"), uid, uid);
                
                // Apply emergency shutdown (makes IPC unconscious/critical)
                _state.ChangeMobState(uid, MobState.Critical, mobState);
                component.LastOverheatKnockdown = _timing.CurTime;
            }
        }
    }
}

