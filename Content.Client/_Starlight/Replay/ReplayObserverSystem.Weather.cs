using Content.Client.Replay.Spectator;
using Content.Client.Weather;
using Content.Shared._Starlight.Replay;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Replay;

// Weather overlay toggle, as admins have in the sandbox panel. The setting lives on WeatherSystem, so it survives
// observer re-spawns; it's restored on playback stop so a replay choice doesn't carry into live play.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private WeatherSystem _weather = default!;

    private static readonly EntProtoId _weatherActionProto = "ActionReplayToggleWeather";

    private EntityUid? _weatherAction;
    private bool? _drawWeatherBeforeReplay;

    private void OnToggleWeather(EntityUid uid, ReplaySpectatorComponent component, ReplayToggleWeatherActionEvent args)
    {
        if (args.Handled || !IsReplayActive)
            return;

        args.Handled = true;
        _drawWeatherBeforeReplay ??= _weather.DrawWeather;
        _weather.DrawWeather = !_weather.DrawWeather;
        _actions.SetToggled(_weatherAction, !_weather.DrawWeather);

        var msg = _weather.DrawWeather
            ? Loc.GetString("replay-observer-weather-on")
            : Loc.GetString("replay-observer-weather-off");
        _popup.PopupEntity(msg, uid);
    }

    private void AddWeatherAction(EntityUid observer)
    {
        _weatherAction = null;
        _actions.AddAction(observer, ref _weatherAction, _weatherActionProto);
        _actions.SetToggled(_weatherAction, !_weather.DrawWeather);
    }

    private void RestoreWeather()
    {
        if (_drawWeatherBeforeReplay is { } draw)
            _weather.DrawWeather = draw;

        _drawWeatherBeforeReplay = null;
        _weatherAction = null;
    }
}
