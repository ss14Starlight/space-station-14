using Content.Server.Ghost.Roles.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;
using Content.Shared._Starlight.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Content.Server.StationRecords.Systems;
using Content.Shared.StationRecords;
using Content.Server.Station.Systems;
using Robust.Shared.Utility;
using Content.Shared._Starlight.Scaling;
using System.Linq;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Scaling;

public sealed partial class ScalingSystem : SharedScalingSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private StationRecordsSystem _recordsSystem = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, double> _cachedPopulations = new();
    private readonly Dictionary<EntityUid, TimeSpan> _pendingScaling = new();

    double _universalHealthWeight;
    int _populationBase;
    double _securityWeight;
    double _salvageWeight;
    double _centcomWeight;
    private static readonly TimeSpan ScalingRetryInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ScalingRetryTimeout = TimeSpan.FromSeconds(30);
    private TimeSpan _nextScalingRetry;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AntagMonsterScalingComponent, MapInitEvent>(OnMobStartup);
        SubscribeLocalEvent<AntagMonsterScalingComponent, GhostRoleSpawnerUsedEvent>(OnGhostRoleSpawnerUsed);

        _cfg.OnValueChanged(StarlightCCVars.ScalingHealthWeight, UpdateUniversalHealthWeight, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingScaling.Count == 0 || _timing.CurTime < _nextScalingRetry)
            return;

        _nextScalingRetry = _timing.CurTime + ScalingRetryInterval;
        var now = _timing.CurTime;

        foreach (var (mob, expiresAt) in _pendingScaling.ToArray())
        {
            if (!TryComp<AntagMonsterScalingComponent>(mob, out var scalingComp))
            {
                _pendingScaling.Remove(mob);
                continue;
            }

            if (TryScaling(mob, scalingComp) || now >= expiresAt)
                _pendingScaling.Remove(mob);
        }
    }

    private void UpdateUniversalHealthWeight(double universalHealthWeight)
        => _universalHealthWeight = universalHealthWeight;

    private void OnMobStartup(EntityUid mob, AntagMonsterScalingComponent scalingComp, ref MapInitEvent args)
        => QueueScaling(mob);

    private void OnGhostRoleSpawnerUsed(
        EntityUid mob,
        AntagMonsterScalingComponent scalingComp,
        ref GhostRoleSpawnerUsedEvent args)
        => QueueScaling(args.Spawned);

    private void QueueScaling(EntityUid mob)
    {
        if (!TryComp<AntagMonsterScalingComponent>(mob, out var scalingComp))
            return;

        if (TryScaling(mob, scalingComp))
            _pendingScaling.Remove(mob);
        else
            _pendingScaling.TryAdd(mob, _timing.CurTime + ScalingRetryTimeout);
    }

    private bool TryScaling(EntityUid mob, AntagMonsterScalingComponent scalingComp)
    {
        if (scalingComp.IsScaled)
            return true;

        if (!TryComp(mob, out MobThresholdsComponent? thresholdsComp))
            return false;

        var nearestGrid = _stationSystem.GetNearestStation(mob);

        if (nearestGrid.Owner == EntityUid.Invalid)
            return false;

        var station = _stationSystem.GetOwningStation(nearestGrid);

        if (station is not { } stationUid || !UpdatePopulation(stationUid))
            return false;

        if (scalingComp.OriginalThresholds == null)
        {
            scalingComp.OriginalThresholds = new();

            foreach (var threshold in thresholdsComp.Thresholds)
                scalingComp.OriginalThresholds.Add(threshold.Key, threshold.Value);
        }

        if (!ApplyHealthScaling(mob, stationUid, scalingComp, thresholdsComp, _cachedPopulations, _universalHealthWeight))
            return false;

        scalingComp.ScaledMaxHealth = thresholdsComp.Thresholds.Keys.Max();
        scalingComp.IsScaled = true;

        return true;
    }

    private bool UpdatePopulation(EntityUid station)
    {
        _populationBase = _cfg.GetCVar(StarlightCCVars.ScalingPopulationBase);
        _securityWeight = _cfg.GetCVar(StarlightCCVars.ScalingSecurityWeight);
        _salvageWeight = _cfg.GetCVar(StarlightCCVars.ScalingSalvageWeight);
        _centcomWeight = _cfg.GetCVar(StarlightCCVars.ScalingCentcomWeight);

        var crewMembers = _recordsSystem.GetRecordsOfType<GeneralStationRecord>(station).ToList();

        if (crewMembers.Count == 0)
            return false;

        double updatedPopulation = 0;

        foreach (var crewMember in crewMembers)
        {
            _prototypeManager.TryIndex(crewMember.Item2.JobPrototype, out JobPrototype? job);

            if (job == null)
                continue;

            updatedPopulation += job.AntagScalingSecurityWeight * _securityWeight;
            updatedPopulation += job.AntagScalingSalvageWeight * _salvageWeight;
            updatedPopulation += job.AntagScalingCentcomWeight * _centcomWeight;
        }

        _cachedPopulations.GetOrNew(station);
        _cachedPopulations[station] = updatedPopulation - _populationBase;

        return true;
    }
}
