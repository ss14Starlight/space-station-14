using Content.Server._Funkystation.Atmos.Events;
using Content.Server._Funkystation.ReagentFires.Components;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Decals;
using Content.Shared._Funkystation.CCVar;
using Content.Shared._Funkystation.ReagentFires;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Funkystation.ReagentFires.Systems;

public sealed partial class ReagentFireSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = null!;
    [Dependency] private SharedTransformSystem _transform = null!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = null!;
    [Dependency] private IPrototypeManager _prototypeManager = null!;
    [Dependency] private SharedAppearanceSystem _appearance = null!;
    [Dependency] private EntityLookupSystem _lookup = null!;
    [Dependency] private SharedAudioSystem _audio = null!;
    [Dependency] private SharedPointLightSystem _light = null!;
    [Dependency] private DecalSystem _decalSystem = null!;
    [Dependency] private IRobustRandom _random = null!;
    [Dependency] private DamageableSystem _damageable = null!;
    [Dependency] private IConfigurationManager _cfg = null!;
    [Dependency] private InventorySystem _inventory = null!;
    [Dependency] private SharedMapSystem _map = null!;

    private static readonly ProtoId<DamageTypePrototype> _structuralDamage = "Structural";
    private static readonly ProtoId<DamageTypePrototype> _heatDamage = "Heat";
    private static readonly string[] _burntDecals = ["burnt1", "burnt2", "burnt3", "burnt4"];
    private static readonly Vector2i[] _cardinalOffsets = [new(0, 1), new(0, -1), new(1, 0), new(-1, 0)];
    private static readonly AtmosDirection[] _cardinalDirections = [AtmosDirection.North, AtmosDirection.South, AtmosDirection.East, AtmosDirection.West];

    private const float UpdateInterval = 1f;

    private readonly List<(EntityUid Uid, ReagentPuddleFireComponent FireComp, PuddleComponent Puddle, TransformComponent Xform)> _dueFires = [];
    private readonly List<EntityUid> _toExtinguish = [];
    private readonly List<Entity<ReagentPuddleFireComponent>> _exposedPuddles = [];
    private readonly List<Entity<ReagentPuddleFireComponent>> _spreadPuddles = [];
    private readonly HashSet<EntityUid> _standingEntities = [];

    [Dependency] private EntityQuery<MapGridComponent> _gridQuery;
    [Dependency] private EntityQuery<GridAtmosphereComponent> _gridAtmosQuery;
    [Dependency] private EntityQuery<ReagentPuddleFireComponent> _fireQuery;
    [Dependency] private EntityQuery<DamageableComponent> _damageableQuery;
    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery;

    private float _puddleDamageMultiplier = 1.0f;
    private float _fireProtectionEffectiveness = 1.0f;
    private bool _volumeScalingEnabled = true;
    private float _volumeScalingReference = 20f;
    private float _volumeScalingCurve = 1.5f;
    private float _smallPuddleBurnThreshold = 1.0f;
    private float _smallPuddleBurnPercent = 0.5f;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, ReagentFireCVars.PuddleFireDamageMultiplier, value => _puddleDamageMultiplier = value, true);
        Subs.CVar(_cfg, ReagentFireCVars.FireProtectionEffectiveness, value => _fireProtectionEffectiveness = value, true);
        Subs.CVar(_cfg, ReagentFireCVars.VolumeScalingEnabled, value => _volumeScalingEnabled = value, true);
        Subs.CVar(_cfg, ReagentFireCVars.VolumeScalingReference, value => _volumeScalingReference = value, true);
        Subs.CVar(_cfg, ReagentFireCVars.VolumeScalingCurve, value => _volumeScalingCurve = value, true);
        Subs.CVar(_cfg, ReagentFireCVars.SmallPuddleBurnThreshold, value => _smallPuddleBurnThreshold = value, true);
        Subs.CVar(_cfg, ReagentFireCVars.SmallPuddleBurnPercent, value => _smallPuddleBurnPercent = value, true);
        SubscribeLocalEvent<ReagentPuddleFireComponent, ComponentShutdown>(OnFireShutdown);
    }

    private void OnFireShutdown(EntityUid uid, ReagentPuddleFireComponent component, ref ComponentShutdown args)
    {
        if (component.PlayingStream != null)
        {
            _audio.Stop(component.PlayingStream);
            component.PlayingStream = null;
        }

        if (component.FireEffectEntity != null)
        {
            QueueDel(component.FireEffectEntity.Value);
            component.FireEffectEntity = null;
        }
    }

    /// <summary>
    /// 0-1 intensity factor based on solution volume relative to the reference volume
    /// Small puddles burn proportionally weaker instead of matching a full puddle
    /// </summary>
    private float GetVolumeFactor(FixedPoint2 volume)
    {
        if (!_volumeScalingEnabled || _volumeScalingReference <= 0f)
            return 1f;

        var ratio = Math.Clamp(volume.Float() / _volumeScalingReference, 0f, 1f);
        return MathF.Pow(ratio, _volumeScalingCurve);
    }

    private static float GetEffectiveFlammability(ReagentPuddleFireComponent fireComp)
        => fireComp.Flammability * fireComp.VolumeFactor;

    /// <summary>
    /// Temperature a hotspot or tile fire needs to ignite this puddle.
    /// Uses the cached volume factor so tiny puddles need a hotter tile to ignite.
    /// </summary>
    private static float GetIgnitionTemperature(ReagentPuddleFireComponent fireComp)
        => 573.15f - (50f * GetEffectiveFlammability(fireComp));

    /// <summary>
    /// Refreshes the cached flammability data of a puddle from its solution.
    /// </summary>
    /// <returns>False if the solution is no longer flammable.</returns>
    private bool RefreshFireState(ReagentPuddleFireComponent fireComp, Solution solution)
    {
        var flammability = solution.GetSolutionFlammability(_prototypeManager);
        if (flammability <= 0)
            return false;

        fireComp.Flammability = flammability;
        fireComp.SelfOxidizing = solution.IsSolutionSelfOxidizing(_prototypeManager);
        fireComp.VolumeFactor = GetVolumeFactor(solution.Volume);

        var effectiveFlammability = GetEffectiveFlammability(fireComp);
        fireComp.FireState = effectiveFlammability > 10 ? 6 : effectiveFlammability > 5 ? 5 : 4;

        return true;
    }

    private void UpdateFireVisuals(EntityUid uid, ReagentPuddleFireComponent fireComp)
    {
        var fireColor = GetFireColor(fireComp.Flammability);
        if (fireComp.FireEffectEntity is { } fireEffect)
        {
            _appearance.SetData(fireEffect, ReagentPuddleFireVisuals.FireState, fireComp.FireState);
            _appearance.SetData(fireEffect, ReagentPuddleFireVisuals.FireColor, fireColor);
        }

        if (TryComp<PointLightComponent>(uid, out var light))
        {
            _light.SetRadius(uid, MathF.Max(2f, fireComp.FireState - 1f), light);
            _light.SetColor(uid, fireColor, light);
        }
    }

    public void UpdateFire(Entity<PuddleComponent> ent)
    {
        if (ent.Comp.Solution == null)
            return;

        var solution = ent.Comp.Solution.Value.Comp.Solution;

        // Puddle solutions change constantly (spreading, evaporation), don't add anything to non-flammable ones.
        if (!_fireQuery.TryComp(ent, out var fireComp))
        {
            if (solution.GetSolutionFlammability(_prototypeManager) <= 0)
                return;

            fireComp = AddComp<ReagentPuddleFireComponent>(ent);
        }

        if (!RefreshFireState(fireComp, solution))
        {
            Extinguish(ent);
            return;
        }

        if (fireComp.OnFire)
            UpdateFireVisuals(ent, fireComp);
    }

    [SubscribeLocalEvent]
    private void OnTileExposed(Entity<TransformComponent> ent, ref TileExposedEvent args)
    {
        if (Count<ReagentPuddleFireComponent>() == 0 || !_gridQuery.TryComp(ent.Owner, out var grid))
            return;

        _exposedPuddles.Clear();
        CollectIgnitablePuddles(ent.Owner, grid, args.Tile, args.Temperature, _exposedPuddles);

        foreach (var puddle in _exposedPuddles)
        {
            Ignite(puddle, puddle.Comp);
            _atmos.GetTileMixture(ent.Owner, null, args.Tile, excite: true);
        }
    }

    [SubscribeLocalEvent]
    private void OnPuddleTileFire(Entity<PuddleComponent> ent, ref TileFireEvent args)
    {
        if (_fireQuery.TryComp(ent, out var fireComp)
            && !fireComp.OnFire
            && args.Temperature >= GetIgnitionTemperature(fireComp))
            Ignite(ent.Owner, fireComp);
    }

    /// <summary>
    /// Collects flammable puddles on a tile that are not burning yet.
    /// Puddles are always anchored, so this doesn't need a spatial lookup.
    /// </summary>
    /// <param name="temperature">If set, only puddles that ignite at this temperature are collected.</param>
    private void CollectIgnitablePuddles(EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        float? temperature,
        List<Entity<ReagentPuddleFireComponent>> puddles)
    {
        var anchored = _map.GetAnchoredEntities(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (!_fireQuery.TryComp(ent, out var fireComp) || fireComp.OnFire)
                continue;

            if (temperature is { } temp && temp < GetIgnitionTemperature(fireComp))
                continue;

            puddles.Add((ent.Value, fireComp));
        }
    }

    private static Color GetFireColor(int flammability)
        => flammability switch
        {
            <= 1 => Color.FromHex("#FF5500"),
            2 => Color.FromHex("#FF9000"),
            3 => Color.FromHex("#FFD000"),
            4 => Color.FromHex("#FFFFE0"),
            _ => Color.FromHex("#FFFFFF")
        };

    private void Ignite(EntityUid uid, ReagentPuddleFireComponent? fireComp = null)
    {
        if (!Resolve(uid, ref fireComp))
            return;

        if (fireComp.OnFire)
            return;

        fireComp.OnFire = true;
        // First burn happens a full interval after ignition.
        fireComp.Accumulator = 0f;

        if (fireComp.PlayingStream == null)
        {
            var audio = _audio.PlayPvs(fireComp.LoopingSound, uid, AudioParams.Default.WithLoop(true).WithVolume(-5f));
            if (audio != null)
            {
                fireComp.PlayingStream = audio.Value.Entity;
            }
        }

        var fireColor = GetFireColor(fireComp.Flammability);

        var light = EnsureComp<PointLightComponent>(uid);
        _light.SetEnabled(uid, true, light);
        _light.SetRadius(uid, MathF.Max(2f, fireComp.FireState - 1f), light);
        _light.SetColor(uid, fireColor, light);
        _light.SetEnergy(uid, 2f, light);

        if (fireComp.FireEffectEntity == null)
        {
            var xform = Transform(uid);
            var fireEnt = Spawn("ReagentPuddleFireEffect", xform.Coordinates);
            _transform.SetParent(fireEnt, uid);
            fireComp.FireEffectEntity = fireEnt;
        }

        _appearance.SetData(fireComp.FireEffectEntity.Value, ReagentPuddleFireVisuals.FireState, fireComp.FireState);
        _appearance.SetData(fireComp.FireEffectEntity.Value, ReagentPuddleFireVisuals.FireColor, fireColor);
    }

    private void Extinguish(EntityUid uid)
    {
        if (!_fireQuery.TryComp(uid, out var fireComp))
            return;

        fireComp.OnFire = false;

        if (fireComp.PlayingStream != null)
        {
            _audio.Stop(fireComp.PlayingStream);
            fireComp.PlayingStream = null;
        }

        RemComp<PointLightComponent>(uid);

        if (fireComp.FireEffectEntity != null)
        {
            QueueDel(fireComp.FireEffectEntity.Value);
            fireComp.FireEffectEntity = null;
        }

        RemComp<ReagentPuddleFireComponent>(uid);
    }

    private float GetFireProtectionReduction(EntityUid uid)
    {
        if (!TryComp<InventoryComponent>(uid, out var inv))
            return 0f;

        var survivalFactor = 1f;
        foreach (var slot in inv.Slots)
        {
            if (!_inventory.TryGetSlotEntity(uid, slot.Name, out var slotEnt, inv))
                continue;

            if (TryComp<FireProtectionComponent>(slotEnt, out var protection))
                survivalFactor *= 1f - Math.Clamp(protection.Reduction, 0f, 1f);
        }

        return 1f - survivalFactor;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _dueFires.Clear();
        _toExtinguish.Clear();

        // Collect first: burning, igniting and damaging can add or remove fire components.
        var query = EntityQueryEnumerator<ReagentPuddleFireComponent, PuddleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var fireComp, out var puddle, out var xform))
        {
            fireComp.Accumulator += frameTime;
            if (fireComp.Accumulator < UpdateInterval)
                continue;

            fireComp.Accumulator -= UpdateInterval;
            _dueFires.Add((uid, fireComp, puddle, xform));
        }

        foreach (var (uid, fireComp, puddle, xform) in _dueFires)
        {
            // An earlier fire in this loop could have deleted the puddle or put it out.
            if (fireComp.Deleted || TerminatingOrDeleted(uid))
                continue;

            if (fireComp.OnFire)
                ProcessBurningPuddle(uid, fireComp, puddle, xform);
            else
                TryAutoIgnite(uid, fireComp, xform);
        }

        foreach (var uid in _toExtinguish)
        {
            Extinguish(uid);
        }
    }

    /// <summary>
    /// Ignites a flammable puddle if the air above it is hot enough.
    /// </summary>
    private void TryAutoIgnite(EntityUid uid, ReagentPuddleFireComponent fireComp, TransformComponent xform)
    {
        if (fireComp.Flammability <= 0 || xform.GridUid is not { } gridUid)
            return;

        var ambientPos = _transform.GetGridTilePositionOrDefault((uid, xform));
        var ambientMix = _atmos.GetTileMixture(gridUid, null, ambientPos, excite: false);
        if (ambientMix == null)
            return;

        // factor volume into auto-ignition too
        var autoIgnitionTemp = 773.15f - (50f * GetEffectiveFlammability(fireComp));
        if (ambientMix.Temperature < autoIgnitionTemp
            || (!fireComp.SelfOxidizing && ambientMix.GetMoles(Gas.Oxygen) <= 0.1f))
            return;

        Ignite(uid, fireComp);
        _atmos.GetTileMixture(gridUid, null, ambientPos, excite: true);
    }

    private void ProcessBurningPuddle(EntityUid uid, ReagentPuddleFireComponent fireComp, PuddleComponent puddle, TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid)
        {
            _toExtinguish.Add(uid);
            return;
        }

        var tilePos = _transform.GetGridTilePositionOrDefault((uid, xform));
        var tileMix = _atmos.GetTileMixture(gridUid, null, tilePos, excite: true);

        var oxygenMoles = tileMix?.GetMoles(Gas.Oxygen) ?? 0f;
        if (!fireComp.SelfOxidizing && oxygenMoles <= 0.1f)
        {
            _toExtinguish.Add(uid);
            return;
        }

        if (!_solutionContainerSystem.ResolveSolution(uid, puddle.SolutionName, ref puddle.Solution, out var solution))
        {
            _toExtinguish.Add(uid);
            return;
        }

        var burnFraction = 0.05f / MathF.Pow(MathF.Max(1f, fireComp.Flammability), 3f);

        var currentVolume = solution.Volume.Float();
        if (currentVolume > 0f && currentVolume < _smallPuddleBurnThreshold)
        {
            var acceleratedFraction = _smallPuddleBurnPercent / MathF.Max(1f, fireComp.Flammability);
            burnFraction = MathF.Max(burnFraction, acceleratedFraction);
        }

        _solutionContainerSystem.BurnFlammableReagents(puddle.Solution.Value, burnFraction);

        // Refresh the cache with the live volume and flammability.
        if (fireComp.Deleted || !RefreshFireState(fireComp, solution))
        {
            _toExtinguish.Add(uid);
            return;
        }

        UpdateFireVisuals(uid, fireComp);

        var effectiveFlammability = GetEffectiveFlammability(fireComp);

        if (tileMix != null)
        {
            // use effectiveFlammability for heat output
            var maxTemp = Atmospherics.T0C + (100f * MathF.Pow(effectiveFlammability, 1.5f));
            if (tileMix.Temperature < maxTemp)
            {
                var heatRate = 10f * effectiveFlammability;
                tileMix.Temperature = MathF.Min(tileMix.Temperature + heatRate, maxTemp);
            }

            if (!fireComp.SelfOxidizing)
            {
                var burnAmount = MathF.Min(0.2f * effectiveFlammability, oxygenMoles);
                tileMix.AdjustMoles(Gas.Oxygen, -burnAmount);
                tileMix.AdjustMoles(Gas.CarbonDioxide, burnAmount * 0.6f);
                tileMix.AdjustMoles(Gas.WaterVapor, burnAmount * 0.8f);
            }
            else
            {
                var burnAmount = 0.2f * effectiveFlammability;
                tileMix.AdjustMoles(Gas.CarbonDioxide, burnAmount * 0.6f);
                tileMix.AdjustMoles(Gas.WaterVapor, burnAmount * 0.8f);
            }
        }

        TryAddBurntDecal(gridUid, tilePos);
        SpreadToAdjacentTiles(gridUid, tilePos, tileMix);
        DamageStandingEntities(uid, gridUid, tilePos, tileMix, effectiveFlammability);
    }

    private void TryAddBurntDecal(EntityUid gridUid, Vector2i tilePos)
    {
        // Only roll the decal lookup when we would actually place a decal.
        if (!_random.Prob(0.25f))
            return;

        var tileBurntDecals = 0;
        foreach (var set in _decalSystem.GetDecalsInRange(gridUid, tilePos))
        {
            if (Array.IndexOf(_burntDecals, set.Decal.Id) == -1)
                continue;

            if (++tileBurntDecals >= 4)
                return;
        }

        _decalSystem.TryAddDecal(_burntDecals[_random.Next(_burntDecals.Length)],
            new EntityCoordinates(gridUid, tilePos),
            out _,
            cleanable: true);
    }

    private void SpreadToAdjacentTiles(EntityUid gridUid, Vector2i tilePos, GasMixture? tileMix)
    {
        var radiatedTemp = tileMix is { Temperature: > Atmospherics.FireMinimumTemperatureToSpread }
            ? tileMix.Temperature * Atmospherics.FireSpreadRadiosityScale
            : 0f;

        Entity<GridAtmosphereComponent?> gridAtmos = (gridUid, _gridAtmosQuery.CompOrNull(gridUid));
        _gridQuery.TryComp(gridUid, out var grid);
        _spreadPuddles.Clear();

        foreach (var offset in _cardinalOffsets)
        {
            var adjacentPos = tilePos + offset;

            // Radiate heat to adjacent tiles unless something airtight (walls, doors, windows) is in the way.
            if (radiatedTemp > 0f
                && gridAtmos.Comp != null
                && _atmos.GetTileMixture(gridUid, null, adjacentPos) is { } adjMix
                && adjMix.Temperature < radiatedTemp
                && !IsAnyAirBlocked(gridAtmos, adjacentPos))
            {
                adjMix.Temperature = radiatedTemp;
                // Only wake up the tiles we actually changed.
                _atmos.GetTileMixture(gridUid, null, adjacentPos, excite: true);
            }

            if (grid != null)
                CollectIgnitablePuddles(gridUid, grid, adjacentPos, null, _spreadPuddles);
        }

        foreach (var adjPuddle in _spreadPuddles)
        {
            Ignite(adjPuddle, adjPuddle.Comp);
        }
    }

    /// <summary>
    /// Whether any airtight entity (wall, closed door, window, thindow...) blocks air on this tile, using atmos' cached data.
    /// </summary>
    private bool IsAnyAirBlocked(Entity<GridAtmosphereComponent?> gridAtmos, Vector2i tile)
    {
        foreach (var direction in _cardinalDirections)
        {
            if (_atmos.IsTileAirBlockedCached(gridAtmos, tile, direction))
                return true;
        }

        return false;
    }

    private void DamageStandingEntities(EntityUid uid, EntityUid gridUid, Vector2i tilePos, GasMixture? tileMix, float effectiveFlammability)
    {
        _standingEntities.Clear();
        _lookup.GetLocalEntitiesIntersecting(gridUid, tilePos, _standingEntities, 0f);
        _standingEntities.Remove(uid);

        if (_standingEntities.Count == 0)
            return;

        // use effectiveFlammability for damage output
        var damageAmount = FixedPoint2.New(2f * effectiveFlammability * _puddleDamageMultiplier);
        var totalDamage = new DamageSpecifier();
        totalDamage.DamageDict.Add(_structuralDamage, damageAmount);
        totalDamage.DamageDict.Add(_heatDamage, damageAmount);

        var fireVolume = 50f * effectiveFlammability;
        var fireEvent = new TileFireEvent(tileMix?.Temperature ?? (Atmospherics.T0C + (50f * effectiveFlammability)), fireVolume);

        foreach (var ent in _standingEntities)
        {
            if (TerminatingOrDeleted(ent))
                continue;

            if (!_xformQuery.TryComp(ent, out var entXform))
                continue;

            if (_transform.GetGridTilePositionOrDefault((ent, entXform)) != tilePos)
                continue;

            if (_damageableQuery.HasComp(ent))
            {
                var ignoreResistances = !_mobStateQuery.HasComp(ent);

                var appliedDamage = totalDamage;
                if (!ignoreResistances)
                {
                    var reduction = Math.Clamp(GetFireProtectionReduction(ent) * _fireProtectionEffectiveness, 0f, 1f);
                    appliedDamage = totalDamage * (1f - reduction);
                }

                _damageable.TryChangeDamage(ent, appliedDamage, ignoreResistances: ignoreResistances);
            }

            if (TerminatingOrDeleted(ent))
                continue;

            RaiseLocalEvent(ent, ref fireEvent);
        }
    }
}
