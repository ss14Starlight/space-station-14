using Content.Shared.Mobs;
using Content.Shared.Movement.Components;

// ReSharper disable CheckNamespace
namespace Content.Shared.Movement.Systems;

public abstract partial class SharedMoverController
{
    private void OnRelayTargetMobStateChanged(
        EntityUid uid,
        MovementRelayTargetComponent component,
        ref MobStateChangedEvent args)
    {
        if (_mobState.IsIncapacitated(uid))
            StopRelayMovement(uid);
    }
}
