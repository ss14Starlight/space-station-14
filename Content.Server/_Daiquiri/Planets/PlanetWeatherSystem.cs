using Content.Server.Chat.Managers;
using Content.Server.Electrocution;
using Content.Shared._Daiquiri.Planets;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Light.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weather;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Daiquiri.Planets;

public sealed partial class PlanetWeatherSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedWeatherSystem _weather = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private ElectrocutionSystem _electrocution = default!;

    private static readonly TimeSpan CalmMin = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CalmMax = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan StormMin = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan StormMax = TimeSpan.FromMinutes(3);

    private const float StrikeMinDistance = 3f;
    private const float StrikeMaxDistance = 14f;

    private const float StrikeHitRadius = 0.9f;

    private static readonly TimeSpan StrikeStunTime = TimeSpan.FromSeconds(3);

    public void StartTracking(EntityUid mapUid, PlanetDestinationPrototype planet)
    {
        var comp = EnsureComp<PlanetWeatherComponent>(mapUid);
        comp.Weathers = new List<EntProtoId>(planet.Weathers);
        comp.Current = null;
        comp.CurrentWeather = null;
        comp.StartMessage = planet.WeatherStartMessage;
        comp.EndMessage = planet.WeatherEndMessage;
        comp.MessageColor = planet.WeatherColor;
        comp.DamageType = planet.WeatherDamageType;
        comp.DamagePerSecond = planet.WeatherDamagePerSecond;
        comp.Lightning = planet.Lightning;
        comp.LightningShockDamage = planet.LightningShockDamage;
        comp.Active = false;
        comp.StormEnd = TimeSpan.Zero;
        comp.DamageAccumulator = 0f;
        comp.NextStorm = _timing.CurTime + RandomBetween(CalmMin, CalmMax);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<PlanetWeatherComponent>();

        while (query.MoveNext(out var mapUid, out var comp))
        {
            if (comp.Active && now >= comp.StormEnd)
                EndStorm(mapUid, comp);
            else if (!comp.Active && now >= comp.NextStorm)
                StartStorm(mapUid, comp);

            if (comp.Current is not { } current)
                continue;

            if (!_weather.HasWeather(Transform(mapUid).MapID, current))
                continue;

            DamageOpenSky(mapUid, comp, frameTime);
            TryStrike(mapUid, comp, frameTime);
        }
    }

    private void StartStorm(EntityUid mapUid, PlanetWeatherComponent comp)
    {
        if (comp.Weathers.Count == 0)
        {
            comp.NextStorm = _timing.CurTime + RandomBetween(CalmMin, CalmMax);
            return;
        }

        var duration = RandomBetween(StormMin, StormMax);
        var weather = _random.Pick(comp.Weathers);

        if (!_weather.TryAddWeather(mapUid, weather, out var weatherEnt, duration))
        {
            comp.NextStorm = _timing.CurTime + RandomBetween(CalmMin, CalmMax);
            return;
        }

        comp.Current = weather;
        comp.CurrentWeather = weatherEnt;
        comp.Active = true;
        comp.StormEnd = _timing.CurTime + duration;
        comp.DamageAccumulator = 0f;

        var announcement = GetAnnouncement(weatherEnt);
        Announce(mapUid, announcement?.StartMessage ?? comp.StartMessage, announcement?.Color ?? comp.MessageColor);
    }

    private void EndStorm(EntityUid mapUid, PlanetWeatherComponent comp)
    {
        var announcement = GetAnnouncement(comp.CurrentWeather);

        if (comp.Current is { } current)
            _weather.TryRemoveWeather(mapUid, current);

        comp.Current = null;
        comp.CurrentWeather = null;
        comp.Active = false;
        comp.NextStorm = _timing.CurTime + RandomBetween(CalmMin, CalmMax);
        Announce(mapUid, announcement?.EndMessage ?? comp.EndMessage, announcement?.Color ?? comp.MessageColor);
    }

    private PlanetWeatherAnnouncementComponent? GetAnnouncement(EntityUid? weather)
    {
        if (weather is not { } uid || TerminatingOrDeleted(uid))
            return null;

        return TryComp<PlanetWeatherAnnouncementComponent>(uid, out var announcement) ? announcement : null;
    }

    private void TryStrike(EntityUid mapUid, PlanetWeatherComponent comp, float frameTime)
    {
        if (comp.Lightning is not { } bolt || comp.CurrentWeather is not { } weather)
            return;

        if (TerminatingOrDeleted(weather))
            return;

        if (!TryComp<PlanetWeatherLightningComponent>(weather, out var lightning))
            return;

        // A chance per second, rolled per frame, which is the same thing and needs no clock.
        if (lightning.ChancePerSecond <= 0f ||
            _random.NextFloat() > lightning.ChancePerSecond * frameTime)
        {
            return;
        }

        if (PickStrikeSpot(mapUid) is not { } spot)
            return;

        Spawn(bolt, spot);
        ShockAround(mapUid, spot, comp.LightningShockDamage);
    }

    private EntityCoordinates? PickStrikeSpot(EntityUid mapUid)
    {
        var outInTheOpen = new List<EntityCoordinates>();

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } mob)
                continue;

            var xform = Transform(mob);

            if (xform.MapUid != mapUid || IsSheltered(mapUid, xform))
                continue;

            outInTheOpen.Add(xform.Coordinates);
        }

        if (outInTheOpen.Count == 0)
            return null;

        return _random.Pick(outInTheOpen).Offset(_random.NextVector2(StrikeMinDistance, StrikeMaxDistance));
    }

    private void ShockAround(EntityUid mapUid, EntityCoordinates spot, float amount)
    {
        if (amount <= 0f)
            return;

        var damage = (int) amount;

        var mobs = EntityQueryEnumerator<MobStateComponent, TransformComponent>();

        while (mobs.MoveNext(out var uid, out var state, out var xform))
        {
            if (state.CurrentState == MobState.Dead || xform.MapUid != mapUid || IsSheltered(mapUid, xform))
                continue;

            if ((xform.Coordinates.Position - spot.Position).Length() > StrikeHitRadius)
                continue;

            _electrocution.TryDoElectrocution(uid, null, damage, StrikeStunTime, refresh: false);
        }
    }

    private void DamageOpenSky(EntityUid mapUid, PlanetWeatherComponent comp, float frameTime)
    {
        if (comp.DamagePerSecond <= 0f)
            return;

        comp.DamageAccumulator += frameTime;

        if (comp.DamageAccumulator < 1f)
            return;

        var ticks = (int) comp.DamageAccumulator;
        comp.DamageAccumulator -= ticks;

        var damage = new DamageSpecifier(
            _prototype.Index(comp.DamageType),
            FixedPoint2.New(comp.DamagePerSecond * ticks));

        var mobs = EntityQueryEnumerator<MobStateComponent, DamageableComponent, TransformComponent>();

        while (mobs.MoveNext(out var uid, out var state, out var damageable, out var xform))
        {
            if (state.CurrentState == MobState.Dead)
                continue;

            if (xform.MapUid != mapUid || IsSheltered(mapUid, xform))
                continue;

            _damage.TryChangeDamage((uid, damageable), damage);
        }
    }

    private bool IsSheltered(EntityUid mapUid, TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid)
            return false;

        if (gridUid != mapUid)
            return true;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        if (!_map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tile))
            return false;

        TryComp<RoofComponent>(gridUid, out var roof);

        return !_weather.CanWeatherAffect(
            new Entity<MapGridComponent?, RoofComponent?>(gridUid, grid, roof),
            tile);
    }

    private void Announce(EntityUid mapUid, LocId? message, Color color)
    {
        if (message == null)
            return;

        var text = Loc.GetString(message.Value);
        var filter = Filter.Empty();

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } mob)
                continue;

            if (Transform(mob).MapUid != mapUid)
                continue;

            filter.AddPlayer(session);
        }

        if (filter.Count == 0)
            return;

        _chat.ChatMessageToManyFiltered(filter, ChatChannel.Server, text, text, mapUid, false, false,
            color);
    }

    private TimeSpan RandomBetween(TimeSpan min, TimeSpan max)
    {
        var span = (max - min).TotalSeconds;
        return TimeSpan.FromSeconds(min.TotalSeconds + _random.NextFloat() * span);
    }
}