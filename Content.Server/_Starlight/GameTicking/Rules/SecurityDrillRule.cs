using System.Linq;
using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.AlertLevel;
using Content.Server.StationEvents.Components;
using Content.Server.StationRecords.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.StationRecords;
using Robust.Shared.Random;

namespace Content.Server.StationEvents.Events;

public sealed class SecurityDrillRule : StationEventSystem<SecurityDrillRuleComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StationRecordsSystem _recordsSystem = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;

    protected override void Added(EntityUid uid, SecurityDrillRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        if (!TryComp<StationEventComponent>(uid, out var stationEvent))
            return;

        var station = stationEvent.TargetStation;
        if (station is null && !TryGetRandomStation(out station))
            return;

        if (!TryComp<AlertLevelComponent>(station, out var alert))
            return;

        if (alert.CurrentLevel != component.RequiredAlertLevel)
        {
            stationEvent.StartAnnouncement = _loc.GetString(component.FailAnnouncement);
            base.Added(uid, component, gameRule, args);
            return;
        }

        if (_random.Prob(component.BasicDrillChance))
        {
            stationEvent.StartAnnouncement = _loc.GetString(component.BasicDrillLocKey,
                ("drill", _random.Pick(component.BasicDrillVariants)));
        }
        else
        {
            var crew = _recordsSystem.GetRecordsOfType<GeneralStationRecord>(station.Value).ToArray();
            if (crew.Length == 0)
                return;

            var target = _random.Pick(crew).Item2.Name;

            stationEvent.StartAnnouncement = _random.Prob(component.DetainChance)
                ? _loc.GetString(component.DetainLocKey, ("target", target))
                : _loc.GetString(component.QuestioningLocKey,
                    ("target", target),
                    ("drill", _random.Pick(component.QuestioningVariants)));
        }
        base.Added(uid, component, gameRule, args);
    }
}
