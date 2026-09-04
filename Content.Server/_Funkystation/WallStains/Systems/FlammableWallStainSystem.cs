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
    [Dependency] private EntityLookupSystem _lookup = null!;
    [Dependency] private SharedAppearanceSystem _appearance = null!;
    [Dependency] private SharedMapSystem _map = null!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MapGridComponent, TileExposedEvent>(OnTileExposed);
        SubscribeLocalEvent<FlammableWallStainComponent, TileFireEvent>(OnTileFire);
        SubscribeLocalEvent<FlammableWallStainComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(EntityUid uid, FlammableWallStainComponent component, ref ComponentShutdown args)
    {
        Extinguish(uid, component);
    }

    private void OnTileExposed(EntityUid gridUid, MapGridComponent component, ref TileExposedEvent args)
    {
        var fireTile = args.Tile;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var toIgnite = new List<(EntityUid Stain, FlammableWallStainComponent Comp)>();

        var offsets = new[] { Vector2i.Zero, new Vector2i(0, 1), new Vector2i(0, -1), new Vector2i(1, 0), new Vector2i(-1, 0) };
        foreach (var offset in offsets)
        {
            var wallTile = fireTile + offset;
            var enumerator = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, wallTile);

            while (enumerator.MoveNext(out var ent))
            {
                var children = Transform(ent.Value).ChildEnumerator;
                while (children.MoveNext(out var child))
                {
                    if (TryComp<FlammableWallStainComponent>(child, out var fireComp) && !fireComp.OnFire &&
                        TryComp<WallStainComponent>(child, out var stain))
                    {
                        if (wallTile + stain.Direction == fireTile || offset == Vector2i.Zero)
                        {
                            if (_solution.TryGetSolution(child, stain.SolutionName, out var solComp))
                                fireComp.Flammability = solComp.Value.Comp.Solution.GetSolutionFlammability(_proto);
                            else
                                fireComp.Flammability = 0;

                            if (fireComp.Flammability <= 0)
                                continue;

                            var ignitionTemp = 573.15f - (50f * fireComp.Flammability);
                            if (args.Temperature >= ignitionTemp)
                                toIgnite.Add((child, fireComp));
                        }
                    }
                }
            }
        }

        foreach (var (stainUid, fireComp) in toIgnite)
        {
            Ignite(stainUid, fireComp);
        }
    }

    private void OnTileFire(EntityUid uid, FlammableWallStainComponent component, ref TileFireEvent args)
    {
        if (component.OnFire)
            return;

        if (TryComp<WallStainComponent>(uid, out var stain) &&
            _solution.TryGetSolution(uid, stain.SolutionName, out var solComp))
        {
            component.Flammability = solComp.Value.Comp.Solution.GetSolutionFlammability(_proto);
        }
        else
        {
            component.Flammability = 0;
        }

        if (component.Flammability <= 0f)
            return;

        var ignitionTemp = 573.15f - (50f * component.Flammability);
        if (args.Temperature >= ignitionTemp)
            Ignite(uid, component);
    }

    private Color GetFireColor(int flammability)
    {
        return flammability switch
        {
            <= 1 => Color.FromHex("#FF5500"),
            2 => Color.FromHex("#FF9000"),
            3 => Color.FromHex("#FFD000"),
            4 => Color.FromHex("#FFFFE0"),
            _ => Color.FromHex("#FFFFFF")
        };
    }

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

    private void Extinguish(EntityUid uid, FlammableWallStainComponent fireComp)
    {
        if (!fireComp.OnFire)
            return;

        fireComp.OnFire = false;

        RemCompDeferred<ActiveFlammableWallStainComponent>(uid);

        RemComp<PointLightComponent>(uid);

        if (fireComp.PlayingStream != null)
        {
            _audio.Stop(fireComp.PlayingStream);
            fireComp.PlayingStream = null;
        }

        fireComp.CurrentPlayingSound = null;

        if (fireComp.FireEffectEntity != null)
        {
            QueueDel(fireComp.FireEffectEntity.Value);
            fireComp.FireEffectEntity = null;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var activeStains = new List<(EntityUid Uid, FlammableWallStainComponent FireComp, WallStainComponent Stain, TransformComponent Xform)>();

        var query = EntityQueryEnumerator<ActiveFlammableWallStainComponent, FlammableWallStainComponent, WallStainComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var fireComp, out var stain, out var xform))
        {
            activeStains.Add((uid, fireComp, stain, xform));
        }

        foreach (var (uid, currentFireComp, currentStain, currentXform) in activeStains)
        {
            if (Deleted(uid))
                continue;

            if (!_solution.TryGetSolution(uid, currentStain.SolutionName, out var solComp))
                continue;

            var flammability = solComp.Value.Comp.Solution.GetSolutionFlammability(_proto);
            var selfOxidizing = solComp.Value.Comp.Solution.IsSolutionSelfOxidizing(_proto);
            currentFireComp.Flammability = flammability;

            if (flammability <= 0)
            {
                Extinguish(uid, currentFireComp);
                continue;
            }

            var gridId = currentXform.GridUid;
            if (gridId == null)
                continue;

            var wallPos = _transform.GetGridTilePositionOrDefault((uid, currentXform));
            var atmosTilePos = wallPos + currentStain.Direction;

            currentFireComp.Accumulator += frameTime;
            if (currentFireComp.Accumulator < 1f)
                continue;
            currentFireComp.Accumulator -= 1f;

            var tileMix = _atmos.GetTileMixture(gridId.Value, null, atmosTilePos, excite: true);
            var currentOxygen = tileMix?.GetMoles(Gas.Oxygen) ?? 0f;

            if (!selfOxidizing && currentOxygen <= 0.1f)
            {
                Extinguish(uid, currentFireComp);
                continue;
            }

            var burnFraction = 0.05f / MathF.Pow(MathF.Max(1f, flammability), 3f);
            _solution.BurnFlammableReagents(solComp.Value, burnFraction);

            if (tileMix != null)
            {
                var maxTemp = Atmospherics.T0C + 100f * MathF.Pow(flammability, 1.5f);
                if (tileMix.Temperature < maxTemp)
                    tileMix.Temperature = MathF.Min(tileMix.Temperature + 10f * flammability, maxTemp);

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

            var entities = new HashSet<EntityUid>();
            _lookup.GetLocalEntitiesIntersecting(gridId.Value, atmosTilePos, entities, 0f);
            foreach (var ent in entities)
            {
                if (HasComp<PuddleComponent>(ent))
                {
                    var fireEvent = new TileFireEvent(tileMix?.Temperature ?? 600f, 50f * flammability);
                    RaiseLocalEvent(ent, ref fireEvent);
                }
            }

            var spreadOffsets = new[] { Vector2i.Zero, new Vector2i(0, 1), new Vector2i(0, -1), new Vector2i(1, 0), new Vector2i(-1, 0) };
            if (TryComp<MapGridComponent>(gridId.Value, out var grid))
            {
                var adjacentStainsToIgnite = new List<(EntityUid, FlammableWallStainComponent)>();

                foreach (var offset in spreadOffsets)
                {
                    var checkWallTile = wallPos + offset;
                    var enumerator = _map.GetAnchoredEntitiesEnumerator(gridId.Value, grid, checkWallTile);
                    while (enumerator.MoveNext(out var ent))
                    {
                        var children = Transform(ent.Value).ChildEnumerator;
                        while (children.MoveNext(out var child))
                        {
                            if (child == uid)
                                continue;

                            if (TryComp<FlammableWallStainComponent>(child, out var adjacentFire) && !adjacentFire.OnFire)
                            {
                                if (TryComp<WallStainComponent>(child, out var adjacentStain) &&
                                    _solution.TryGetSolution(child, adjacentStain.SolutionName, out var adjSol))
                                {
                                    adjacentFire.Flammability = adjSol.Value.Comp.Solution.GetSolutionFlammability(_proto);
                                }
                                else
                                {
                                    adjacentFire.Flammability = 0;
                                }

                                if (adjacentFire.Flammability > 0)
                                    adjacentStainsToIgnite.Add((child, adjacentFire));
                            }
                        }
                    }
                }

                foreach (var (stainUid, fireCompAdjacent) in adjacentStainsToIgnite)
                {
                    Ignite(stainUid, fireCompAdjacent);
                }
            }
        }
    }
}
