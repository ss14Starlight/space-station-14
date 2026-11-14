using System.Numerics;
using Content.Shared._Starlight.Teleportation.Systems;
using Content.Shared.Salvage.Fulton;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Salvage;

/// <summary>
/// Transports attached entities to the linked beacon after a timer has elapsed.
/// </summary>
public sealed partial class FultonSystem : SharedFultonSystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTeleportSystem _teleports = default!; // Starlight

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FultonedComponent, ComponentStartup>(OnFultonedStartup);
        SubscribeLocalEvent<FultonedComponent, ComponentShutdown>(OnFultonedShutdown);
    }

    private void OnFultonedShutdown(EntityUid uid, FultonedComponent component, ComponentShutdown args)
    {
        Del(component.Effect);
        component.Effect = EntityUid.Invalid;
    }

    private void OnFultonedStartup(EntityUid uid, FultonedComponent component, ComponentStartup args)
    {
        if (Exists(component.Effect))
            return;

        component.Effect = Spawn(EffectProto, new EntityCoordinates(uid, EffectOffset));
        Dirty(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FultonedComponent>();
        var curTime = Timing.CurTime;

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextFulton > curTime)
                continue;

            Fulton(uid, comp);
        }
    }

    private void Fulton(EntityUid uid, FultonedComponent component)
    {
        if (!Deleted(component.Beacon) &&
            TryComp(component.Beacon, out TransformComponent? beaconXform) &&
            !Container.IsEntityOrParentInContainer(component.Beacon.Value, xform: beaconXform) &&
            CanFulton(uid))
        {
            var xform = Transform(uid);
            var metadata = MetaData(uid);
            var oldCoords = xform.Coordinates;
            var offset = _random.NextVector2(1.5f);
            var localPos = Vector2.Transform(
                    TransformSystem.GetWorldPosition(beaconXform),
                    TransformSystem.GetInvWorldMatrix(beaconXform.ParentUid)) + offset;

            // Starlight start: teleport helper
            //TransformSystem.SetCoordinates(uid, new EntityCoordinates(beaconXform.ParentUid, localPos));
            _teleports.Teleport((uid, xform), new EntityCoordinates(beaconXform.ParentUid, localPos), new SharedTeleportSystem.TeleportOptions
            {
                CarryPulled = SharedTeleportSystem.DefaultTeleportCarryPulled,
                CarryMassLimit = SharedTeleportSystem.DefaultTeleportCarryMassLimit,
                CarryIncomingPulls = SharedTeleportSystem.DefaultTeleportCarryIncomingPulls,
            });
            // Starlight end

            RaiseNetworkEvent(new FultonAnimationMessage()
            {
                Entity = GetNetEntity(uid, metadata),
                Coordinates = GetNetCoordinates(oldCoords),
            });
        }

        Audio.PlayPvs(component.Sound, uid);
        RemCompDeferred<FultonedComponent>(uid);
    }
}
