using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Trigger;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.FireDragon;

public sealed partial class DragonFireSystem : EntitySystem
{
    private const float HalfTile = 0.5f;
    private const float ExistingFireSearchRadius = 0.1f;
    private const float MinimumTileSpacing = 0.01f;

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private NpcFactionSystem _factions = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private OccluderSystem _occluder = default!;

    // A player's fixture can intersect several burning tiles. Share the contact
    // cooldown across tiles, including flames spawned at different times.
    private readonly Dictionary<EntityUid, TimeSpan> _nextContactDamage = new();
    private readonly List<EntityUid> _expiredContacts = new();

    // Contact damage can trigger events that spawn fire, so the lookup buffers
    // must remain separate while the contact targets are being enumerated.
    private readonly HashSet<EntityUid> _contactTargets = new();
    private readonly HashSet<EntityUid> _nearbyFireEntities = new();

    [SubscribeLocalEvent]
    private void OnFireInit(Entity<DragonGroundFireComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.ExpiresAt = _timing.CurTime + ent.Comp.Lifetime;
        ent.Comp.NextDamage = _timing.CurTime;
    }

    [SubscribeLocalEvent]
    private void OnExtinguished(Entity<DragonGroundFireComponent> ent, ref ExtinguishedEvent args) => QueueDel(ent);

    [SubscribeLocalEvent]
    private void OnExtinguish(Entity<DragonGroundFireComponent> ent, ref ExtinguishEvent args)
    {
        if (args.FireStacksAdjustment < 0)
            QueueDel(ent);
    }

    [SubscribeLocalEvent]
    private void OnPreventCollide(Entity<AncientFlameProjectileComponent> ent, ref PreventCollideEvent args)
    {
        if (TryComp<ProjectileComponent>(ent, out var projectile)
            && projectile.Shooter is { } source
            && Exists(source)
            && _factions.IsEntityFriendly(source, args.OtherEntity))
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnImpact(Entity<AncientFlameProjectileComponent> ent, ref TriggerEvent args)
    {
        if (ent.Comp.Impacted)
            return;

        ent.Comp.Impacted = true;
        var origin = _transform.GetMapCoordinates(ent);
        var source = Comp<ProjectileComponent>(ent).Shooter;
        if (!_map.TryFindGridAt(origin, out var gridUid, out var grid))
            return;

        foreach (var tile in _map.GetTilesIntersecting(gridUid, grid,
                    new Circle(origin.Position, ent.Comp.ImpactFireRadius), false))
        {
            var center = _map.GridTileToWorld(gridUid, grid, tile.GridIndices);
            if (_occluder.InRangeUnoccluded(origin, center, 0f, ignoreTouching: true))
                SpawnFire(gridUid, grid, tile.GridIndices, source, ent.Comp.GroundFirePrototype);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        RemoveExpiredContactCooldowns(now);
        UpdateGroundFires(now);
        UpdateProjectileTrails(now);
        UpdateFireWaves(now);
    }

    private void RemoveExpiredContactCooldowns(TimeSpan now)
    {
        _expiredContacts.Clear();
        foreach (var (target, nextDamage) in _nextContactDamage)
        {
            if (nextDamage <= now || !Exists(target))
                _expiredContacts.Add(target);
        }

        foreach (var target in _expiredContacts)
            _nextContactDamage.Remove(target);
    }

    private void UpdateGroundFires(TimeSpan now)
    {
        var fires = EntityQueryEnumerator<DragonGroundFireComponent, TransformComponent>();
        while (fires.MoveNext(out var uid, out var fire, out var xform))
        {
            if (now >= fire.ExpiresAt)
            {
                QueueDel(uid);
                continue;
            }

            if (now < fire.NextDamage)
                continue;

            fire.NextDamage = now + fire.DamageInterval;
            var center = _transform.GetMapCoordinates(uid, xform);
            _contactTargets.Clear();
            _lookup.GetEntitiesInRange(center.MapId, center.Position, fire.ContactRange, _contactTargets);
            foreach (var target in _contactTargets)
                ApplyContactDamage((uid, fire), target, now);
        }
    }

    private void ApplyContactDamage(Entity<DragonGroundFireComponent> ent, EntityUid target, TimeSpan now)
    {
        if (!HasComp<MobStateComponent>(target)
            || (_nextContactDamage.TryGetValue(target, out var nextDamage) && now < nextDamage)
            || _containers.IsEntityInContainer(target)
            || HasComp<FireDragonComponent>(target)
            || (ent.Comp.Source is { } source && Exists(source) && _factions.IsEntityFriendly(source, target)))
            return;

        _nextContactDamage[target] = now + ent.Comp.DamageInterval;
        _damage.TryChangeDamage(target, ent.Comp.Damage, origin: ent.Comp.Source);

        if (!TryComp<FlammableComponent>(target, out var flammable))
            return;

        _flammable.AdjustFireStacks(target, ent.Comp.FireStacks, flammable);
        _flammable.Ignite(target, ent, flammable, ignitionSourceUser: ent.Comp.Source);
    }

    private void UpdateProjectileTrails(TimeSpan now)
    {
        var projectiles = EntityQueryEnumerator<AncientFlameProjectileComponent, ProjectileComponent, TransformComponent>();
        while (projectiles.MoveNext(out var uid, out var trail, out var projectile, out var xform))
        {
            if (trail.Impacted || now < trail.NextTrail)
                continue;

            trail.NextTrail = now + trail.TrailInterval;
            var position = _transform.GetMapCoordinates(uid, xform);
            var previous = trail.LastPosition is { } last && last.MapId == position.MapId ? last : position;
            // Interpolate so fast projectiles and long frames cannot leave gaps in the trail.
            var spacing = Math.Max(trail.TrailSampleSpacing, MinimumTileSpacing);
            var samples = Math.Clamp(
                (int) MathF.Ceiling(Vector2.Distance(previous.Position, position.Position) / spacing),
                1,
                Math.Max(1, trail.MaxTrailSamples));
            for (var i = 1; i <= samples; i++)
            {
                var coords = new MapCoordinates(Vector2.Lerp(previous.Position, position.Position, (float) i / samples), position.MapId);
                SpawnTrailTile(trail, projectile.Shooter, coords);
            }
            trail.LastPosition = position;
        }
    }

    private void SpawnTrailTile(AncientFlameProjectileComponent trail, EntityUid? source, MapCoordinates coords)
    {
        if (!_map.TryFindGridAt(coords, out var gridUid, out var grid))
            return;

        var tile = _map.CoordinatesToTile(gridUid, grid, coords);
        if (!trail.BurnedTiles.Add((gridUid, tile)))
            return;

        SpawnFire(gridUid, grid, tile, source, trail.GroundFirePrototype);
    }

    private void UpdateFireWaves(TimeSpan now)
    {
        var waves = EntityQueryEnumerator<DragonFireWaveComponent>();
        while (waves.MoveNext(out var uid, out var wave))
        {
            if (now < wave.NextStep)
                continue;

            if (!TryComp<MapGridComponent>(wave.Grid, out var grid) || IsWaveFinished(wave))
            {
                QueueDel(uid);
                continue;
            }

            if (wave.Rings)
            {
                var origin = _map.GridTileToWorld(wave.Grid, grid, wave.Origin);
                AdvanceRingWave(wave, grid, origin, now);
                continue;
            }

            var breathOrigin = _transform.ToMapCoordinates(new EntityCoordinates(wave.Grid, wave.BreathOrigin));
            AdvanceBreathWave(wave, grid, breathOrigin, now);
        }
    }

    private static bool IsWaveFinished(DragonFireWaveComponent wave) =>
        wave.Rings ? wave.Step >= wave.Length : wave.Distance >= wave.Length;

    private void AdvanceRingWave(DragonFireWaveComponent wave, MapGridComponent grid, MapCoordinates origin, TimeSpan now)
    {
        wave.Step++;
        for (var x = -wave.Step; x <= wave.Step; x++)
        for (var y = -wave.Step; y <= wave.Step; y++)
        {
            if (Math.Abs(x) != wave.Step && Math.Abs(y) != wave.Step)
                continue;

            SpawnWaveTile(wave, grid, origin, wave.Origin + new Vector2i(x, y));
        }

        wave.NextStep = now + wave.Interval;
    }

    private void AdvanceBreathWave(DragonFireWaveComponent wave, MapGridComponent grid, MapCoordinates origin, TimeSpan now)
    {
        // Advance in small tile fractions so diagonal and arbitrary angles fill
        // smoothly instead of snapping entire cardinal rows.
        var stepDistance = Math.Clamp(wave.BreathStepDistance, MinimumTileSpacing, 1f);
        wave.Distance = Math.Min(wave.Length, wave.Distance + stepDistance);
        var front = wave.Distance + HalfTile;
        while (wave.NextBreathPosition < wave.BreathPositions.Count
            && wave.BreathPositions[wave.NextBreathPosition].Distance <= front)
        {
            var position = wave.BreathPositions[wave.NextBreathPosition++].Position;
            SpawnBreathPosition(wave, grid, origin, position);
        }

        wave.NextStep = now + (wave.Interval * stepDistance);
    }

    private void SpawnWaveTile(DragonFireWaveComponent wave, MapGridComponent grid, MapCoordinates origin, Vector2i tile)
    {
        var coords = _map.GridTileToWorld(wave.Grid, grid, tile);
        if (_occluder.InRangeUnoccluded(origin, coords, 0f, ignoreTouching: true))
            SpawnFire(wave.Grid, grid, tile, wave.Source, wave.GroundFirePrototype, wave.GroundFireLifetimeOverride);
    }

    private void SpawnBreathPosition(DragonFireWaveComponent wave, MapGridComponent grid, MapCoordinates origin, Vector2 position)
    {
        var coords = new EntityCoordinates(wave.Grid, position);
        if (_occluder.InRangeUnoccluded(origin, _transform.ToMapCoordinates(coords), 0f, ignoreTouching: true))
            SpawnFire(wave.Grid, grid, coords, wave.Source, wave.GroundFirePrototype, wave.GroundFireLifetimeOverride);
    }

    private void SpawnFire(EntityUid gridUid, MapGridComponent grid, Vector2i tile, EntityUid? source,
        EntProtoId prototype, TimeSpan? lifetimeOverride = null) =>
        SpawnFire(gridUid, grid, _map.GridTileToLocal(gridUid, grid, tile), source, prototype, lifetimeOverride);

    private void SpawnFire(EntityUid gridUid, MapGridComponent grid, EntityCoordinates coords, EntityUid? source,
        EntProtoId prototype, TimeSpan? lifetimeOverride = null)
    {
        var tile = _map.CoordinatesToTile(gridUid, grid, coords);
        if (_map.GetTileRef(gridUid, grid, tile).Tile.IsEmpty)
            return;

        _nearbyFireEntities.Clear();
        _lookup.GetEntitiesInRange(coords, ExistingFireSearchRadius, _nearbyFireEntities);
        foreach (var existing in _nearbyFireEntities)
        {
            if (!TryComp<DragonGroundFireComponent>(existing, out var fire))
                continue;

            var expiresAt = _timing.CurTime + (lifetimeOverride ?? fire.Lifetime);
            if (expiresAt > fire.ExpiresAt)
                fire.ExpiresAt = expiresAt;

            return;
        }

        var uid = Spawn(prototype, coords);
        var newFire = Comp<DragonGroundFireComponent>(uid);
        newFire.Source = source;
        // Keep the nominal lifetime for later breath or projectile refreshes.
        if (lifetimeOverride is { } lifetime)
            newFire.ExpiresAt = _timing.CurTime + lifetime;
    }
}
