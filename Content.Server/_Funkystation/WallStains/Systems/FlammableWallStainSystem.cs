using Content.Server._Funkystation.Atmos.Events;
using Content.Server._Funkystation.WallStains.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Funkystation.ReagentFires;
using Content.Shared._Funkystation.WallStains.Components;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Fluids.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Funkystation.WallStains.Systems;

public sealed partial class FlammableWallStainSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = null!;
    [Dependency] private SharedTransformSystem _transform = null!;
    [Dependency] private SharedSolutionContainerSystem _solution = null!;
    [Dependency] private IPrototypeManager _proto = null!;
    [Dependency] private DamageableSystem _damageable = null!;
    [Dependency] private SharedAudioSystem _audio = null!;
    [Dependency] private SharedPointLightSystem _light = null!;
    [Dependency] private SharedAppearanceSystem _appearance = null!;
    [Dependency] private SharedMapSystem _map = null!;

    private static readonly Vector2i[] _tileAndCardinalOffsets = [Vector2i.Zero, new(0, 1), new(0, -1), new(1, 0), new(-1, 0)];

    // Starlight - reused collections, these used to be allocated for every exposure / every tick.
    private readonly List<(EntityUid Stain, FlammableWallStainComponent Comp)> _toIgnite = [];
    private readonly List<(EntityUid Uid, FlammableWallStainComponent FireComp, WallStainComponent Stain, TransformComponent Xform)> _activeStains = [];

    [Dependency] private EntityQuery<StainedWallComponent> _stainedWallQuery;
    [Dependency] private EntityQuery<FlammableWallStainComponent> _fireQuery;
    [Dependency] private EntityQuery<WallStainComponent> _stainQuery;
    [Dependency] private EntityQuery<PuddleComponent> _puddleQuery;

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<FlammableWallStainComponent> ent, ref ComponentShutdown _)
        => Extinguish(ent);

    [SubscribeLocalEvent]
    private void OnTileExposed(Entity<MapGridComponent> ent, ref TileExposedEvent args)
    {
        // Starlight - nothing to ignite if there are no wall stains at all.
        if (Count<FlammableWallStainComponent>() == 0)
            return;

        var fireTile = args.Tile;
        _toIgnite.Clear();

        foreach (var offset in _tileAndCardinalOffsets)
        {
            var wallTile = fireTile + offset;
            var enumerator = _map.GetAnchoredEntities(ent.Owner, ent.Comp, wallTile);

            while (enumerator.MoveNext(out var wall))
            {
                // Starlight - only stained walls have stain children.
                if (!_stainedWallQuery.HasComp(wall))
                    continue;

                var children = Transform(wall.Value).ChildEnumerator;
                while (children.MoveNext(out var child))
                {
                    if (_fireQuery.TryComp(child, out var fireComp) && !fireComp.OnFire &&
                        _stainQuery.TryComp(child, out var stain))
                    {
                        if (wallTile + stain.Direction == fireTile || offset == Vector2i.Zero)
                        {
                            fireComp.Flammability = _solution.TryGetSolution(child, stain.SolutionName, out var solComp)
                                ? solComp.Value.Comp.Solution.GetSolutionFlammability(_proto)
                                : 0;

                            if (fireComp.Flammability <= 0)
                                continue;

                            var ignitionTemp = 573.15f - (50f * fireComp.Flammability);
                            if (args.Temperature >= ignitionTemp)
                                _toIgnite.Add((child, fireComp));
                        }
                    }
                }
            }
        }

        foreach (var (stainUid, fireComp) in _toIgnite)
            Ignite(stainUid, fireComp);
    }

    [SubscribeLocalEvent]
    private void OnTileFire(EntityUid uid, FlammableWallStainComponent component, ref TileFireEvent args)
    {
        if (component.OnFire)
            return;

        component.Flammability = TryComp<WallStainComponent>(uid, out var stain) &&
            _solution.TryGetSolution(uid, stain.SolutionName, out var solComp)
            ? solComp.Value.Comp.Solution.GetSolutionFlammability(_proto)
            : 0;

        if (component.Flammability <= 0f)
            return;

        var ignitionTemp = 573.15f - (50f * component.Flammability);
        if (args.Temperature >= ignitionTemp)
            Ignite(uid, component);
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

    private void Ignite(EntityUid uid, FlammableWallStainComponent fireComp)
    {
        if (fireComp.OnFire || fireComp.Flammability <= 0) // Extra safety check!
            return;

        fireComp.OnFire = true;
        fireComp.FireState = fireComp.Flammability > 10 ? 6 : fireComp.Flammability > 5 ? 5 : 4;
        var fireColor = GetFireColor(fireComp.Flammability);

        EnsureComp<ActiveFlammableWallStainComponent>(uid);

        var light = EnsureComp<PointLightComponent>(uid);
        _light.SetEnabled(uid, true, light);
        _light.SetRadius(uid, MathF.Max(1.5f, fireComp.FireState - 2f), light);
        _light.SetColor(uid, fireColor, light);
        _light.SetEnergy(uid, 1.5f, light);

        var wantedSoundPath = fireComp.Flammability >= 4
            ? "/Audio/_Funkystation/Effects/Fire/hissing.ogg"
            : "/Audio/_Funkystation/Effects/Fire/bigfire.ogg";

        fireComp.PlayingStream = _audio.PlayPvs(new SoundPathSpecifier(wantedSoundPath), uid, AudioParams.Default.WithLoop(true).WithVolume(-8f))?.Entity;
        fireComp.CurrentPlayingSound = wantedSoundPath;

        if (fireComp.FireEffectEntity == null)
        {
            var parentWall = Transform(uid).ParentUid;
            if (parentWall.IsValid())
            {
                var fireEnt = Spawn("WallStainFireEffect", Transform(parentWall).Coordinates);
                _transform.SetParent(fireEnt, parentWall);
                _transform.SetLocalPosition(fireEnt, System.Numerics.Vector2.Zero);
                fireComp.FireEffectEntity = fireEnt;
            }
        }

        if (fireComp.FireEffectEntity is { } fireEntEffect)
        {
            _appearance.SetData(fireEntEffect, ReagentPuddleFireVisuals.FireState, fireComp.FireState);
            _appearance.SetData(fireEntEffect, ReagentPuddleFireVisuals.FireColor, fireColor);
        }
    }

    private void Extinguish(Entity<FlammableWallStainComponent> ent)
    {
        if (!ent.Comp.OnFire)
            return;

        ent.Comp.OnFire = false;

        RemCompDeferred<ActiveFlammableWallStainComponent>(ent.Owner);

        RemComp<PointLightComponent>(ent.Owner);

        if (ent.Comp.PlayingStream != null)
        {
            _audio.Stop(ent.Comp.PlayingStream);
            ent.Comp.PlayingStream = null;
        }

        ent.Comp.CurrentPlayingSound = null;

        if (ent.Comp.FireEffectEntity != null)
        {
            QueueDel(ent.Comp.FireEffectEntity.Value);
            ent.Comp.FireEffectEntity = null;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _activeStains.Clear();

        var query = EntityQueryEnumerator<ActiveFlammableWallStainComponent, FlammableWallStainComponent, WallStainComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var fireComp, out var stain, out var xform))
        {
            // Starlight - stains only burn once a second, don't touch their solutions every tick.
            fireComp.Accumulator += frameTime;
            if (fireComp.Accumulator < 1f)
                continue;
            fireComp.Accumulator -= 1f;

            _activeStains.Add((uid, fireComp, stain, xform));
        }

        foreach (var (uid, currentFireComp, currentStain, currentXform) in _activeStains)
        {
            if (Deleted(uid))
                continue;

            if (!_solution.TryGetSolution(uid, currentStain.SolutionName, out var solComp))
                continue;

            var flammability = solComp.Value.Comp.Solution.GetSolutionFlammability(_proto);
            currentFireComp.Flammability = flammability;

            if (flammability <= 0)
            {
                Extinguish((uid, currentFireComp));
                continue;
            }

            var selfOxidizing = solComp.Value.Comp.Solution.IsSolutionSelfOxidizing(_proto);

            var gridId = currentXform.GridUid;
            if (gridId == null)
                continue;

            var wallPos = _transform.GetGridTilePositionOrDefault((uid, currentXform));
            var atmosTilePos = wallPos + currentStain.Direction;

            var tileMix = _atmos.GetTileMixture(gridId.Value, null, atmosTilePos, excite: true);
            var currentOxygen = tileMix?.GetMoles(Gas.Oxygen) ?? 0f;

            if (!selfOxidizing && currentOxygen <= 0.1f)
            {
                Extinguish((uid, currentFireComp));
                continue;
            }

            var burnFraction = 0.05f / MathF.Pow(MathF.Max(1f, flammability), 3f);
            _solution.BurnFlammableReagents(solComp.Value, burnFraction);

            if (tileMix != null)
            {
                var maxTemp = Atmospherics.T0C + (100f * MathF.Pow(flammability, 1.5f));
                if (tileMix.Temperature < maxTemp)
                    tileMix.Temperature = MathF.Min(tileMix.Temperature + (10f * flammability), maxTemp);

                var burnAmount = selfOxidizing ? 0.2f * flammability : MathF.Min(0.2f * flammability, currentOxygen);
                if (!selfOxidizing)
                    tileMix.AdjustMoles(Gas.Oxygen, -burnAmount);
                tileMix.AdjustMoles(Gas.CarbonDioxide, burnAmount * 0.6f);
                tileMix.AdjustMoles(Gas.WaterVapor, burnAmount * 0.8f);
            }

            currentFireComp.FireState = flammability > 10 ? 6 : flammability > 5 ? 5 : 4;
            var fireColor = GetFireColor(currentFireComp.Flammability);

            if (currentFireComp.FireEffectEntity is { } fireEnt)
            {
                _appearance.SetData(fireEnt, ReagentPuddleFireVisuals.FireState, currentFireComp.FireState);
                _appearance.SetData(fireEnt, ReagentPuddleFireVisuals.FireColor, fireColor);
            }

            var wantedSoundPath = flammability >= 4
                ? "/Audio/_Funkystation/Effects/Fire/hissing.ogg"
                : "/Audio/_Funkystation/Effects/Fire/bigfire.ogg";

            if (currentFireComp.CurrentPlayingSound != wantedSoundPath)
            {
                if (currentFireComp.PlayingStream != null)
                    _audio.Stop(currentFireComp.PlayingStream);

                currentFireComp.PlayingStream = _audio.PlayPvs(new SoundPathSpecifier(wantedSoundPath), uid, AudioParams.Default.WithLoop(true).WithVolume(-8f))?.Entity;
                currentFireComp.CurrentPlayingSound = wantedSoundPath;
            }

            if (TryComp<PointLightComponent>(uid, out var light))
            {
                _light.SetRadius(uid, MathF.Max(1.5f, currentFireComp.FireState - 2f), light);
                _light.SetColor(uid, fireColor, light);
            }

            if (flammability >= 4)
            {
                var parent = currentXform.ParentUid;
                if (parent.IsValid() && HasComp<DamageableComponent>(parent))
                {
                    var damage = new DamageSpecifier();
                    damage.DamageDict.Add("Structural", 2.5f * flammability);
                    damage.DamageDict.Add("Heat", 1.5f * flammability);
                    _damageable.TryChangeDamage(parent, damage, ignoreResistances: true);
                }
            }

            if (!TryComp<MapGridComponent>(gridId.Value, out var grid))
                continue;

            // Starlight - puddles are anchored, no need for a spatial lookup.
            var fireEvent = new TileFireEvent(tileMix?.Temperature ?? 600f, 50f * flammability);
            var puddles = _map.GetAnchoredEntities(gridId.Value, grid, atmosTilePos);
            while (puddles.MoveNext(out var ent))
            {
                if (_puddleQuery.HasComp(ent))
                    RaiseLocalEvent(ent.Value, ref fireEvent);
            }

            _toIgnite.Clear();

            foreach (var offset in _tileAndCardinalOffsets)
            {
                var checkWallTile = wallPos + offset;
                var enumerator = _map.GetAnchoredEntities(gridId.Value, grid, checkWallTile);
                while (enumerator.MoveNext(out var ent))
                {
                    if (!_stainedWallQuery.HasComp(ent))
                        continue;

                    var children = Transform(ent.Value).ChildEnumerator;
                    while (children.MoveNext(out var child))
                    {
                        if (child == uid)
                            continue;

                        if (_fireQuery.TryComp(child, out var adjacentFire) && !adjacentFire.OnFire)
                        {
                            adjacentFire.Flammability = _stainQuery.TryComp(child, out var adjacentStain) &&
                                _solution.TryGetSolution(child, adjacentStain.SolutionName, out var adjSol)
                                ? adjSol.Value.Comp.Solution.GetSolutionFlammability(_proto)
                                : 0;

                            if (adjacentFire.Flammability > 0)
                                _toIgnite.Add((child, adjacentFire));
                        }
                    }
                }
            }

            foreach (var (stainUid, fireCompAdjacent) in _toIgnite)
            {
                Ignite(stainUid, fireCompAdjacent);
            }
        }
    }
}
