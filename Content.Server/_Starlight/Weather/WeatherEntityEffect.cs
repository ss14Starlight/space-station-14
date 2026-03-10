using Content.Shared.EntityEffects;
using Content.Shared.Mobs.Components;
using Content.Shared.Weather;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Weather;

public sealed class WeatherEntityEffect : EntitySystem
{
    [Dependency] private readonly SharedEntityEffectsSystem _entityEffects = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedWeatherSystem _weather = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize() => base.Initialize();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WeatherComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_timing.CurTime < component.NextUpdate)
                continue;

            component.NextUpdate = _timing.CurTime + component.UpdateCooldown;

            foreach (var (id, data) in component.Weather)
            {
                if (data.State != WeatherState.Running)
                    continue;

                var weather = _prototypeManager.Index(id);

                if (weather == null || weather.Effects == null)
                    continue;

                var mobquery = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
                while (mobquery.MoveNext(out var mob, out var _, out var xform))
                {
                    var gridUid = _transform.GetGrid(xform.Coordinates);
                    if (gridUid is not null)
                    {
                        if (TryComp<MapGridComponent>(gridUid, out var grid))
                        {
                            var tile = _map.GetTileRef((gridUid.Value, grid), xform.Coordinates);
                            if (!_weather.CanWeatherAffect(gridUid.Value, grid, tile, weather.OnlySpace, weather.CheckTileWeather))
                                continue;
                        }
                    }

                    _entityEffects.ApplyEffects(mob, weather.Effects.ToArray(), user: mob);
                }
            }
        }
    }
}
