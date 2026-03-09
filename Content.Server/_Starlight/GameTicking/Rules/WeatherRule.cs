using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Weather;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.StationEvents.Events;

public sealed class WeatherRule : StationEventSystem<WeatherRuleComponent>
{
    [Dependency] private readonly SharedWeatherSystem _weather = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    protected override void Started(EntityUid uid, WeatherRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        EntityUid? chosenStation = null;
        if (!TryComp<StationEventComponent>(uid, out var stationEvent)) return;
        chosenStation = stationEvent.TargetStation;
        if (chosenStation is null)
            if (!TryGetRandomStation(out chosenStation))
                return;

        if (!_prototypeManager.Resolve(comp.Weather, out var weatherPrototype))
            return;

        if (_station.GetLargestGrid(chosenStation.Value) is not { } grid)
            return;
                
        comp.Map = Transform(grid).MapID;

        Timer.Spawn(comp.Delay, () => _weather.SetWeather(comp.Map, weatherPrototype, null));
    }

    protected override void Ended(EntityUid uid, WeatherRuleComponent comp, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, comp, gameRule, args);

        _weather.SetWeather(comp.Map, null, null);
    }
}
