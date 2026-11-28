using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Projectiles;

// ReSharper disable CheckNamespace
namespace Content.Shared.Movement.Systems;

public abstract partial class SharedMoverController
{
    [Dependency] private EntityQuery<EmbeddableProjectileComponent> _embeddableQuery = default!;

    private bool IsEmbeddedProjectile(EntityUid uid)
        => _embeddableQuery.TryComp(uid, out var embeddable) && embeddable.EmbeddedIntoUid != null;

    private void OnRelayTargetMobStateChanged(
        EntityUid uid,
        MovementRelayTargetComponent component,
        ref MobStateChangedEvent args)
    {
        if (_mobState.IsIncapacitated(uid))
            StopRelayMovement(uid);
    }
}
