using System.Linq;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Access.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Station;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Builds a name-only infection list from suit sensors in vitals or coordinates mode.
/// Coordinates are deliberately absent from the networked detector state.
/// </summary>
public sealed partial class PathogenDetectorSystem : EntitySystem
{
    [Dependency] private PathogenContaminationSourceSystem _sources = default!;
    [Dependency] private PathogenRegistrySystem _registry = default!;
    [Dependency] private SharedIdCardSystem _idCards = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PathogenDetectorComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<PathogenDetectorComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
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
        var entries = new List<PathogenDetectorEntry>();
        var seenHosts = new HashSet<EntityUid>();
        var station = _station.GetOwningStation(observer);

        var query = EntityQueryEnumerator<SuitSensorComponent>();
        while (query.MoveNext(out _, out var sensor))
        {
            if (sensor.Mode < SuitSensorMode.SensorVitals ||
                sensor.User is not { } host ||
                !seenHosts.Add(host) ||
                station is not null && sensor.StationId != station ||
                !TryComp<PathogenInfectionComponent>(host, out var infections))
            {
                continue;
            }

            foreach (var infection in infections.Infections)
            {
                if (!_registry.TryGetStrain(infection.Pathogen, out var strain))
                    continue;

                var detection = strain.Identification == PathogenIdentificationStage.Unidentified
                    ? Loc.GetString("pathogen-detector-unidentified")
                    : Loc.GetString(
                        "pathogen-detector-identified",
                        ("designation", strain.Designation));
                entries.Add(new PathogenDetectorEntry(GetSensorName(host), detection));
            }
        }

        entries = entries
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Detection, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PathogenDetectorUiState(entries, GetContaminationReading(observer));
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

    private string GetContaminationReading(EntityUid observer)
    {
        if (!_sources.TryGetStrongestSource(observer, out var reading))
            return Loc.GetString("pathogen-detector-contamination-none");

        var signatures = string.Join(
            "/",
            reading.PathogenTypes.Select(type =>
                Loc.GetString($"pathogen-contamination-signature-{type.ToString().ToLowerInvariant()}")));
        var key = reading.BeaconName is null
            ? "pathogen-detector-contamination"
            : "pathogen-detector-contamination-located";
        return Loc.GetString(
            key,
            ("type", signatures),
            ("distance", reading.Distance.ToString("0")),
            ("direction", reading.Direction),
            ("beacon", reading.BeaconName ?? string.Empty));
    }
}
