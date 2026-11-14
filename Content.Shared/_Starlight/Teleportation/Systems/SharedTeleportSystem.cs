using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Shared._Starlight.Teleportation.Systems;

public sealed partial class SharedTeleportSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private PullingSystem _pulling = default!;

    public const bool DefaultTeleportCarryPulled = true;
    public const bool DefaultTeleportCarryIncomingPulls = false;
    public const float DefaultTeleportCarryMassLimit = 150f; // Player characters cap out around 115

    public sealed class TeleportOptions
    {
        /// <summary>
        /// Whether anything the entity teleporting is pulling comes along instead of being left behind.
        /// </summary>
        public bool CarryPulled = DefaultTeleportCarryPulled;

        /// <summary>
        /// Whether whoever is pulling the teleported entity comes along instead of being left behind.
        /// </summary>
        public bool CarryIncomingPulls = DefaultTeleportCarryIncomingPulls;

        /// <summary>
        /// Heaviest entity that can come along, in kilograms. Heavier targets stay behind and the pull is broken.
        /// </summary>
        public float CarryMassLimit = DefaultTeleportCarryMassLimit;
    }

    /// <summary>
    /// Teleports an entity, by default bringing whoever they're pulling along with them.
    /// </summary>
    public void Teleport(Entity<TransformComponent?> ent, EntityCoordinates destination, TeleportOptions? options = null)
    {
        options ??= new TeleportOptions();

        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        var passenger = TryComp<PullerComponent>(ent.Owner, out var puller) ? puller.Pulling : null;
        if (options.CarryPulled && passenger is { } target && TryMovePulled(ent.Owner, target, destination, options.CarryMassLimit))
            TrimCarriedLinks(ent.Owner, target);
        else if (passenger is { } leftBehind && TryComp<PullableComponent>(leftBehind, out var leftPullable))
        {
            _pulling.TryStopPull(leftBehind, leftPullable);
        }

        var incoming = TryComp<PullableComponent>(ent.Owner, out var selfPullable) ? selfPullable.Puller : null;
        if (options.CarryIncomingPulls && incoming is { } dragger && TryMovePulled(ent.Owner, dragger, destination, options.CarryMassLimit))
            TrimCarriedLinks(ent.Owner, dragger);
        else if (selfPullable is not null && selfPullable.Puller is not null)
            _pulling.TryStopPull(ent.Owner, selfPullable);

        _transform.SetCoordinates(ent.Owner, destination);
        _transform.AttachToGridOrMap(ent.Owner, ent.Comp);
    }

    /// <summary>
    /// Breaks any of the carried companion's other pull links.
    /// </summary>
    private void TrimCarriedLinks(EntityUid uid, EntityUid companion)
    {
        if (TryComp<PullableComponent>(companion, out var carriedPullable) && carriedPullable.Puller is { } otherPuller && otherPuller != uid)
            _pulling.TryStopPull(companion, carriedPullable);
        if (TryComp<PullerComponent>(companion, out var carriedPuller) && carriedPuller.Pulling is { } third && third != uid && TryComp<PullableComponent>(third, out var thirdPullable))
            _pulling.TryStopPull(third, thirdPullable);
    }

    private bool TryMovePulled(EntityUid uid, EntityUid target, EntityCoordinates destination, float carryMassLimit)
    {
        if (!Exists(target) || Transform(target).Anchored)
            return false;

        if (_transform.GetMapId(target) != _transform.GetMapId(destination.EntityId))
            return false;

        if (TryComp<PhysicsComponent>(target, out var physics) && physics.Mass > carryMassLimit)
            return false;

        var destMap = _transform.ToMapCoordinates(destination);
        var offset = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(uid);
        var passengerDest = _transform.ToCoordinates((destination.EntityId, Transform(destination.EntityId)), new MapCoordinates(destMap.Position + offset, destMap.MapId));
        _transform.SetCoordinates(target, passengerDest);
        _transform.AttachToGridOrMap(target, Transform(target));
        return true;
    }
}
