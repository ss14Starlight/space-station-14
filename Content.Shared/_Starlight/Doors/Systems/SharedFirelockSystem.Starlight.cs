using Content.Shared.Doors.Components;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Doors.Systems;

public abstract partial class SharedFirelockSystem
{
    [SubscribeLocalEvent]
    private void OnDoorStateChanged(EntityUid uid, FirelockComponent component, DoorStateChangedEvent args)
    {
        if (args.State != DoorState.Opening)
            return;

        component.EmergencyCloseCooldown = _gameTiming.CurTime + component.EmergencyCloseCooldownDuration;
    }
}
