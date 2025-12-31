// IPC System - Temperature (Server)
// _STARLIGHT: Temperature-based knockdown for overheating IPCs

using Content.Shared._Starlight.Silicons.IPC.Components;
using Content.Shared.Temperature;
using Robust.Shared.Audio;

namespace Content.Server._Starlight.Silicons.IPC;

public sealed partial class IPCSystem
{
    private const float OverheatThreshold = 335f; // 335K = ~62°C - IPC shuts down to prevent death
    private const float OverheatKnockdownDuration = 8f; // Long shutdown duration when overheated

    private void InitializeTemperature()
    {
        SubscribeLocalEvent<IPCBatteryComponent, OnTemperatureChangeEvent>(OnIPCTemperatureChange);
    }

    private void OnIPCTemperatureChange(EntityUid uid, IPCBatteryComponent component, OnTemperatureChangeEvent args)
    {
        // If temperature exceeds overheat threshold, knock down the IPC
        if (args.CurrentTemperature >= OverheatThreshold)
        {
            // Check if we should apply knockdown (don't spam it)
            var currentTime = _timing.CurTime;
            if (component.LastOverheatKnockdown == null || 
                currentTime - component.LastOverheatKnockdown > TimeSpan.FromSeconds(OverheatKnockdownDuration + 1))
            {
                // Play overheat alarm
                _audio.PlayEntity(new SoundPathSpecifier("/Audio/Weapons/Guns/EmptyAlarm/smg_empty_alarm.ogg"), uid, uid);
                
                // Apply knockdown
                _stun.TryKnockdown(uid, TimeSpan.FromSeconds(OverheatKnockdownDuration), autoStand: true);
                component.LastOverheatKnockdown = currentTime;
            }
        }
    }
}
