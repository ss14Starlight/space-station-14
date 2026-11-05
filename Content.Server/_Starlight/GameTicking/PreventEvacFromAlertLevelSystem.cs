using Content.Server.AlertLevel;
using Content.Server.Communications;
using Content.Server.Station.Systems;

namespace Content.Server._Starlight.GameTicking;

public sealed partial class PreventEvacFromAlertLevelSystem : EntitySystem
{
    
    [Dependency] private AlertLevelSystem _alertLevelSystem = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    
    [SubscribeLocalEvent]
    private void OnShuttleCallAttempt(ref CommunicationConsoleCallShuttleAttemptEvent ev)
    {
        var stationUid = _stationSystem.GetOwningStation(ev.Uid);
        if (stationUid is not { } station) return;
        if (!TryComp<AlertLevelComponent>(station, out var alertLevelComponent))
            return;
        var alertLevelId = _alertLevelSystem.GetLevel(station);
        if (alertLevelComponent.AlertLevels is not {} alertLevels) return;
        if (!alertLevels.Levels.TryGetValue(alertLevelId, out var alertLevel))
            return;

        if (!alertLevel.PreventEvacuation) return;
        
        ev.Cancelled = true;
        ev.Reason = Loc.GetString("alert-level-shuttle-call-unavailable");
    }
}