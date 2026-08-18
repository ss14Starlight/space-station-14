using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Content.Shared.Weather;
using Content.Shared._Starlight.Weather.Components;

namespace Content.Shared._Starlight.Weather.EntitySystems;

/// <summary>
/// Adds weather to a map.
/// </summary>
public sealed partial class WeatherMarkerSystem : EntitySystem
{
    [Dependency] private readonly SharedWeatherSystem _weather = default!;
    [Dependency] private readonly INetManager _netManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WeatherMarkerComponent, MapInitEvent>(OnMapInit);
    }

    /// <summary>
    /// Adds weather to the map on mapinit an optional delay.
    /// </summary>
    private void OnMapInit(EntityUid uid, WeatherMarkerComponent comp, MapInitEvent args)
    {
        var mapId = Transform(uid).MapID;

        // sanity check for invalid map & not despawning in spawn menu
        if (mapId == MapId.Nullspace)
            return;

        // apply weather
        Timer.Spawn(comp.Delay, () => _weather.TryAddWeather(mapId, comp.Weather, out _, comp.Duration));

        // clean up marker afterwards
        QueueDel(uid);
    }
}
