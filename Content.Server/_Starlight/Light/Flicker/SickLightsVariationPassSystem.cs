using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.VariationPass;
using Content.Server.Light.EntitySystems;
using Content.Server._Starlight.Zones;
using Content.Shared._Starlight.Light.Flicker;
using Content.Shared.GameTicking.Components;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Light.Flicker;

public sealed partial class SickLightsVariationPassSystem : VariationPassSystem<SickLightsVariationPassComponent>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private LightBulbSystem _bulb = default!;
    [Dependency] private PoweredLightSystem _poweredLight = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ZoneSystem _zones = default!;

    private readonly List<EntityUid> _ended = [];

    /// <summary>
    /// Schedules failures and randomly assigns persistent flicker to eligible station lights in the configured zone.
    /// </summary>
    protected override void ApplyVariation(Entity<SickLightsVariationPassComponent> ent, ref StationVariationPassEvent args)
    {
        ent.Comp.Station = args.Station;
        ent.Comp.NextFailure = _timing.CurTime + NextDelay(ent.Comp);

        var query = AllEntityQuery<LightFlickerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var flicker, out var xform))
        {
            if (flicker.Override == null
                && IsMemberOfStation((uid, xform), ref args)
                && InZone(xform, ent.Comp)
                && Random.Prob(ent.Comp.SickChance))
                _poweredLight.SetFlickerOverride((uid, flicker), ent.Comp.Flicker);
        }
    }

    /// <summary>
    /// Finishes expired failures and periodically selects a lit fixture for a temporary flicker failure.
    /// </summary>
    protected override void ActiveTick(EntityUid uid, SickLightsVariationPassComponent comp, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, comp, gameRule, frameTime);

        if (comp.Station == null)
            return;

        var now = _timing.CurTime;

        foreach (var (light, end) in comp.Failing)
        {
            if (now >= end)
                _ended.Add(light);
        }

        foreach (var light in _ended)
        {
            comp.Failing.Remove(light);
            EndFailure(light, comp);
        }

        _ended.Clear();

        if (now < comp.NextFailure)
            return;

        comp.NextFailure = now + NextDelay(comp);

        if (PickLight(comp) is not { } picked)
            return;

        var duration = Random.Next(comp.MinDuration, comp.MaxDuration);
        comp.Failing[picked] = now + duration;
        _poweredLight.SetFlickerOverride(picked, comp.Flicker);
    }

    /// <summary>
    /// Clears flicker overrides on surviving fixtures with active temporary failures.
    /// </summary>
    protected override void Ended(EntityUid uid, SickLightsVariationPassComponent comp, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, comp, gameRule, args);

        foreach (var light in comp.Failing.Keys)
        {
            if (!TerminatingOrDeleted(light))
                _poweredLight.SetFlickerOverride(light, null);
        }

        comp.Failing.Clear();
    }

    /// <summary>
    /// Clears a surviving fixture's flicker override and may burn out its working bulb, refreshing the light and playing the burnout sound.
    /// </summary>
    private void EndFailure(EntityUid light, SickLightsVariationPassComponent comp)
    {
        if (TerminatingOrDeleted(light))
            return;

        _poweredLight.SetFlickerOverride(light, null);

        if (!Random.Prob(comp.BurnoutChance)
            || !TryComp<PoweredLightComponent>(light, out var fixture)
            || _poweredLight.GetBulb(light, fixture) is not { } bulb
            || !TryComp<LightBulbComponent>(bulb, out var bulbComp)
            || bulbComp.State != LightBulbState.Normal)
            return;

        _bulb.SetState(bulb, LightBulbState.Burned, bulbComp);
        _poweredLight.UpdateLight(light, fixture);
        _audio.PlayPvs(comp.BurnoutSound, light);
    }

    /// <summary>
    /// Randomly selects a lit, nonflickering fixture in the configured station and zone, or returns null if none qualifies.
    /// </summary>
    private EntityUid? PickLight(SickLightsVariationPassComponent comp)
    {
        EntityUid? picked = null;
        var count = 0;

        var query = EntityQueryEnumerator<LightFlickerComponent, PoweredLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var flicker, out var light, out var xform))
        {
            // A lit light with no flicker of its own: a working bulb on power, not already sick.
            if (flicker.Profile != null
                || flicker.Override != null
                || !_poweredLight.IsLit((uid, light))
                || Stations.GetOwningStation(uid, xform) != comp.Station
                || !InZone(xform, comp))
            {
                continue;
            }

            // Reservoir sampling, one pass with no list.
            count++;
            if (Random.Next(count) == 0)
                picked = uid;
        }

        return picked;
    }

    /// <summary>
    /// Returns whether the fixture occupies the configured zone; returns false without a map grid.
    /// </summary>
    private bool InZone(TransformComponent xform, SickLightsVariationPassComponent comp)
    {
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        return _zones.IsInZone(grid, tile, comp.Zone);
    }

    /// <summary>
    /// Samples the interval before the next failure from the configured delay range.
    /// </summary>
    private TimeSpan NextDelay(SickLightsVariationPassComponent comp)
        => Random.Next(comp.MinDelay, comp.MaxDelay);
}
