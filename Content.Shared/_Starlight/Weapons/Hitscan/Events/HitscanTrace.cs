using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Hitscan.Events;

[Serializable, NetSerializable]
public struct HitscanTrace
{
    public Angle Angle;
    public float Distance;

    public NetCoordinates? MuzzleCoordinates;
    public NetCoordinates? TravelCoordinates;
    public NetCoordinates ImpactCoordinates;
    public NetEntity? ImpactedEnt;
}
