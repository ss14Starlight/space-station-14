using Content.Shared.Random.Rules;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Zones;

public sealed partial class InZoneRule : RulesRule
{
    [DataField(required: true)]
    public List<ProtoId<ZonePrototype>> Zones = [];

    public override bool Check(EntityManager entManager, EntityUid uid) =>
        (!entManager.TryGetComponent(uid, out ZoneTrackerComponent? tracker) ||
        tracker.Zone is not { } zone) ?
        Inverted : Zones.Contains(zone) != Inverted;
}
