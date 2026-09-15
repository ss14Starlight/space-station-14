using Content.Server.Anomaly;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Server._Starlight.StationEvents.Components;
using Content.Shared.Anomaly.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Starlight.StationEvents.Events;

public sealed partial class ResonanceCascadeSchedulerSystem : GameRuleSystem<ResonanceCascadeSchedulerComponent>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AnomalySystem _anomaly = default!;
    [Dependency] private StationSystem _station = default!;

    protected override void Started(EntityUid uid, ResonanceCascadeSchedulerComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        PickAnomalyFamily(component);

        component.StartedAt = Timing.CurTime;
        component.NextSpawnAt = Timing.CurTime + TimeSpan.FromSeconds(Math.Max(0f, component.InitialDelay));
    }

    protected override void ActiveTick(EntityUid uid, ResonanceCascadeSchedulerComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (Timing.CurTime < component.NextSpawnAt)
            return;

        // Re-pick if VV or malformed data clears the selected prototype.
        if (string.IsNullOrWhiteSpace(component.SelectedAnomalyPrototype))
            PickAnomalyFamily(component);

        if (string.IsNullOrWhiteSpace(component.SelectedAnomalyPrototype))
        {
            component.NextSpawnAt = Timing.CurTime + TimeSpan.FromSeconds(Math.Max(component.MinimumInterval, 1f));
            return;
        }

        if (!TryGetRandomStation(out var chosenStation) ||
            !TryComp<StationDataComponent>(chosenStation, out var stationData))
        {
            component.NextSpawnAt = Timing.CurTime + TimeSpan.FromSeconds(Math.Max(component.MinimumInterval, 1f));
            return;
        }

        var grid = _station.GetLargestGrid((chosenStation.Value, stationData));
        if (grid is null)
        {
            component.NextSpawnAt = Timing.CurTime + TimeSpan.FromSeconds(Math.Max(component.MinimumInterval, 1f));
            return;
        }

        if (!TryComp<MapGridComponent>(grid.Value, out var gridComponent))
        {
            component.NextSpawnAt = Timing.CurTime + TimeSpan.FromSeconds(Math.Max(component.MinimumInterval, 1f));
            return;
        }

        var spawned = _anomaly.SpawnOnRandomGridLocationReturning((grid.Value, gridComponent), component.SelectedAnomalyPrototype);

        if (spawned is EntityUid spawnedUid)
        {
            if (HasComp<AnomalyComponent>(spawnedUid))
            {
                _anomaly.StartSupercriticalEvent((spawnedUid, null));
            }
            else
            {
                Log.Warning($"ResonanceCascadeScheduler spawned {ToPrettyString(spawnedUid)} from {component.SelectedAnomalyPrototype}, but it has no AnomalyComponent.");
            }
        }

        var nextInterval = GetCurrentSpawnInterval(component);
        component.NextSpawnAt = Timing.CurTime + TimeSpan.FromSeconds(nextInterval);
    }

    private void PickAnomalyFamily(ResonanceCascadeSchedulerComponent component)
    {
        if (component.PossibleAnomalyPrototypes.Count == 0)
        {
            component.SelectedAnomalyPrototype = string.Empty;
            Log.Warning("ResonanceCascadeScheduler has no possible anomaly prototypes configured.");
            return;
        }

        component.SelectedAnomalyPrototype = _random.Pick(component.PossibleAnomalyPrototypes);
    }

    private float GetCurrentSpawnInterval(ResonanceCascadeSchedulerComponent component)
    {
        var elapsedSeconds = (float) (Timing.CurTime - component.StartedAt).TotalSeconds;
        var step = Math.Max(component.EscalationStep, 1f);
        var elapsedSteps = MathF.Floor(Math.Max(0f, elapsedSeconds) / step);

        var multiplier = component.EscalationMultiplier;
        if (multiplier <= 0f)
            multiplier = 1f;

        var interval = component.BaseInterval * MathF.Pow(multiplier, elapsedSteps);
        return Math.Max(component.MinimumInterval, interval);
    }
}
