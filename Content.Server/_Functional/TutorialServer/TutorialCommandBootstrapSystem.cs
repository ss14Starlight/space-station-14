using Content.Server.AlertLevel;
using Content.Server.Station.Systems;
using Content.Shared._Functional.TutorialServer;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Attaches a station with alert levels to Captain (and other command) practice grids
/// so the communications console can change alert level.
/// </summary>
public sealed partial class TutorialCommandBootstrapSystem : EntitySystem
{
    private static readonly EntProtoId CommandStationProto = "TutorialCommandStation";

    [Dependency] private StationSystem _station = default!;
    [Dependency] private AlertLevelSystem _alertLevel = default!;

    public void TryConfigureOnGrid(EntityUid gridUid, TutorialRolePrototype role)
    {
        if (role.ID is not ("TutorialCaptain" or "TutorialHeadOfPersonnel" or "TutorialHeadOfSecurity"))
            return;

        if (_station.GetOwningStation(gridUid) is { } existing &&
            HasComp<AlertLevelComponent>(existing))
            return;

        var station = Spawn(CommandStationProto, MapCoordinates.Nullspace);
        _station.AddGridToStation(station, gridUid, name: "Tutorial Command");

        // Ensure starting level is Green so setting Blue is a real change.
        if (TryComp<AlertLevelComponent>(station, out var alert))
        {
            _alertLevel.SetLevel(station, "Green", playSound: false, announce: false, force: true); // Starlight edit
        }
    }
}
