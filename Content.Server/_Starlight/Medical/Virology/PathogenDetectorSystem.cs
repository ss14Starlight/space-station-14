using System.Linq;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Access.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Station;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Builds the unified virology monitor state from contamination and suit-sensor data.
/// Sick crew are represented only by sensor names; pathogen and crew-position data are absent.
/// </summary>
public sealed partial class PathogenDetectorSystem : EntitySystem
{
    [Dependency] private PathogenContaminationSystem _contamination = default!;
    [Dependency] private PathogenContaminationSourceSystem _sources = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedIdCardSystem _idCards = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PathogenDetectorComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<PathogenDetectorComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + TimeSpan.FromSeconds(2);
        var query = EntityQueryEnumerator<PathogenDetectorComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!_ui.IsUiOpen(uid, PathogenDetectorUiKey.Key))
                continue;

            foreach (var actor in _ui.GetActors(uid, PathogenDetectorUiKey.Key))
            {
                UpdateState((uid, component), actor);
                break;
            }
        }
    }

    private void OnUseInHand(Entity<PathogenDetectorComponent> detector, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        UpdateState(detector, args.User);
        _ui.TryOpenUi(detector.Owner, PathogenDetectorUiKey.Key, args.User);
        args.Handled = true;
    }

    private void OnBeforeUiOpen(
        Entity<PathogenDetectorComponent> detector,
        ref BeforeActivatableUIOpenEvent args)
    {
        UpdateState(detector, args.User);
    }

    private void UpdateState(Entity<PathogenDetectorComponent> detector, EntityUid observer)
    {
        _ui.SetUiState(
            detector.Owner,
            PathogenDetectorUiKey.Key,
            BuildState(observer));
    }

    public PathogenDetectorUiState BuildState(EntityUid observer)
    {
        var sickCrew = new List<string>();
        var seenHosts = new HashSet<EntityUid>();
        var station = _station.GetOwningStation(observer);

        var query = EntityQueryEnumerator<SuitSensorComponent>();
        while (query.MoveNext(out _, out var sensor))
        {
            if (sensor.Mode < SuitSensorMode.SensorVitals ||
                sensor.User is not { } host ||
                !seenHosts.Add(host) ||
                station is not null && sensor.StationId != station ||
                !TryComp<PathogenInfectionComponent>(host, out var infections) ||
                infections.Infections.Count == 0)
            {
                continue;
            }

            sickCrew.Add(GetSensorName(host));
        }

        sickCrew = sickCrew
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        NetEntity? grid = null;
        var stationName = Loc.GetString("pathogen-monitor-station-unknown");
        var groups = new List<PathogenContaminationBeaconGroup>();
        if (_sources.TryGetBeaconGroups(observer, out var gridUid, out groups))
        {
            grid = GetNetEntity(gridUid);
            stationName = Name(gridUid);
        }

        return new PathogenDetectorUiState(
            grid,
            stationName,
            _contamination.Contamination,
            _contamination.GetContamination(PathogenType.Virus),
            _contamination.GetContamination(PathogenType.Bacteria),
            _contamination.GetContamination(PathogenType.Fungus),
            sickCrew,
            groups);
    }

    private string GetSensorName(EntityUid host)
    {
        if (_idCards.TryFindIdCard(host, out var card) &&
            !string.IsNullOrWhiteSpace(card.Comp.FullName))
        {
            return card.Comp.FullName;
        }

        return Loc.GetString("suit-sensor-component-unknown-name");
    }
}
