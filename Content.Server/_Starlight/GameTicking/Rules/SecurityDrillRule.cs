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

    protected override void Added(EntityUid uid, SecurityDrillRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        EntityUid? chosenStation = null;
        if (!TryComp<StationEventComponent>(uid, out var stationEvent)) return;
        chosenStation = stationEvent.TargetStation;
        if (chosenStation is null)
            if (!TryGetRandomStation(out chosenStation))
                return;

        var str = Loc.GetString("security-drill-event-fail-announcement");

        // Check Alert Level
        if (TryComp<AlertLevelComponent>(chosenStation, out var alert) && alert.CurrentLevel == "green")
        {
            if (_random.Prob(0.2f))
                str = Loc.GetString("security-drill-basic", ("drill", Loc.GetString($"security-drill-basic-{_random.Next(1, 5)}")));
            else
            {
                HashSet<string> target = new();
                var crewMembers = _recordsSystem.GetRecordsOfType<GeneralStationRecord>(chosenStation.Value);
                foreach (var crewMember in crewMembers)
                    target.Add(crewMember.Item2.Name);

                if (_random.Prob(0.3f))
                    str = Loc.GetString("security-drill-detain",
                        ("target", _random.Pick(target)));
                else
                    str = Loc.GetString("security-drill-questioning",
                        ("target", _random.Pick(target)),
                        ("drill", Loc.GetString($"security-drill-questioning-{_random.Next(1, 6)}")));
            }
        }

        stationEvent.StartAnnouncement = str;

        base.Added(uid, component, gameRule, args);
    }
}
