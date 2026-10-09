using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.Shared._Starlight.Weapons.Hitscan.Events;

[ByRefEvent]
public record struct HitscanLagCompensationEvent(
    EntityUid? Shooter,
    EntityUid Ignored,
    MapId MapId,
    Vector2 Origin,
    Vector2 Direction,
    float MaxDistance,
    int CollisionMask,
    List<RayCastResults> Results);
