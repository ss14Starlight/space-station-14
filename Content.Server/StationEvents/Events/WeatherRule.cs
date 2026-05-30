using Content.Server.StationEvents.Components;
using Content.Server.Weather;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.StationEvents.Events;

public sealed class WeatherRule : StationEventSystem<WeatherRuleComponent>
{
    [Dependency] private readonly WeatherSystem _weather = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override void Started(EntityUid uid, WeatherRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        // Starlight - Edited
        EntityUid? chosenStation = null;
        if (!TryComp<StationEventComponent>(uid, out var stationEvent)) return;
        chosenStation = stationEvent.TargetStation;
        if (chosenStation is null)
            if (!TryGetRandomStation(out chosenStation))
                return;

        if (!TryComp<StationDataComponent>(chosenStation, out var stationData))
            return;

        var grid = StationSystem.GetLargestGrid((chosenStation.Value, stationData));

        if (grid is null)
            return;

        component.Map = Transform(grid.Value).MapID; // SL

        // Starlight - Edited
        Timer.Spawn(component.Delay, () => _weather.TryAddWeather(component.Map, component.Weather, out _));
    }

    protected override void Ended(EntityUid uid, WeatherRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        _weather.TryRemoveWeather(component.Map, component.Weather);
    }
}
