using System.Linq;
using Content.Shared.Tag;
using Content.Shared._Starlight.Weapons.Hitscan.Events;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed partial class HitscanBasicRaycastSystem
{
    private static readonly ProtoId<TagPrototype> PersonalEnergyDomeTag = "PersonalEnergyDome";

    /// <returns>True to hit, false to skip, or null to use normal hit selection.</returns>
    private bool? GetPersonalEnergyDomeHitOverride(
        RayCastResults collision,
        EntityUid domeOwner,
        List<HitscanTrace>? previousTraces)
    {
        if (!_tag.HasTag(collision.HitEntity, PersonalEnergyDomeTag))
            return null;

        if (Transform(collision.HitEntity).ParentUid == domeOwner)
            return false;

        if (collision.Distance != 0)
            return null;

        // Penetration can restart inside overlapping domes. Hit each dome only once.
        var dome = GetNetEntity(collision.HitEntity);
        return previousTraces?.Any(trace => trace.ImpactedEnt == dome) != true;
    }
}
