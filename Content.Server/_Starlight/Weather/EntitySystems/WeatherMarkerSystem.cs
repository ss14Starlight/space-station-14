using Robust.Shared.Map;
using Robust.Shared.Timing;
using Content.Server.Weather;
using Content.Server._Starlight.Weather.Components;

namespace Content.Server._Starlight.Weather.EntitySystems;

/// <summary>
/// Adds weather to a map.
/// </summary>
public sealed partial class WeatherMarkerSystem : EntitySystem
{
    [Dependency] private readonly WeatherSystem _weather = default!;

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
        var xform = Transform(uid);

        // sanity check for invalid map
        if (xform.MapID == MapId.Nullspace)
        {
            QueueDel(uid);
            return;
        }


        var mapId = xform.MapID;

        // apply weather
        Timer.Spawn(comp.Delay, () => _weather.TryAddWeather(mapId, comp.Weather, out _, comp.Duration));

        // clean up marker afterwards
        QueueDel(uid);
    }
}
