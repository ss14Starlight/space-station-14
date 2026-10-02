using Content.Server._Funkystation.Atmos.Events;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Funkystation.WallStains.Components;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Fluids.Components;
using Robust.Server.GameObjects;
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
    [Dependency] private EntityQuery<StainedWallComponent> _stainedWallQuery;
    [Dependency] private EntityQuery<FlammableWallStainComponent> _fireQuery;
    [Dependency] private EntityQuery<WallStainComponent> _stainQuery;
    [Dependency] private EntityQuery<PuddleComponent> _puddleQuery;
    private static readonly Vector2i[] _tileAndCardinalOffsets = [Vector2i.Zero, new(0, 1), new(0, -1), new(1, 0), new(-1, 0)];

    // Starlight - reused collections, these used to be allocated for every exposure / every tick.
    private readonly List<(EntityUid Stain, FlammableWallStainComponent Comp)> _toIgnite = [];

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<FlammableWallStainComponent> ent, ref ComponentShutdown _)
    {
        _burningStains.Remove(ent.Owner);
        Extinguish(ent);
    }

    [SubscribeLocalEvent]
    private void OnTileExposed(Entity<MapGridComponent> ent, ref TileExposedEvent args)
    {
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
        if (component.OnFire || component.Flammability <= 0f)
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
        if (fireComp.OnFire || fireComp.Flammability <= 0)
            return;

        fireComp.OnFire = true;
        fireComp.NeedsSpread = true;
        _burningStains.Add(uid);

        UpdateFireVisuals((uid, fireComp));
    }

    private void Extinguish(Entity<FlammableWallStainComponent> ent)
    {
        _burningStains.Remove(ent.Owner);

        if (!ent.Comp.OnFire)
            return;

        ent.Comp.OnFire = false;

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

        if (_burningStains.Count == 0)
            return;

        // Wall-stain fires advance in half-second steps, so skip all work between steps.
        _updateAccumulator += frameTime;
        if (_updateAccumulator < UpdateInterval)
            return;

        _updateAccumulator -= UpdateInterval;

        _burningSnapshot.Clear();
        _burningSnapshot.AddRange(_burningStains);

        foreach (var uid in _burningSnapshot)
        {
            if (!_fireQuery.TryComp(uid, out var currentFireComp) || !currentFireComp.OnFire)
            {
                _burningStains.Remove(uid);
                continue;
            }

            if (!_stainQuery.TryComp(uid, out var currentStain)
                || !_xformQuery.TryComp(uid, out var currentXform))
            {
                Extinguish((uid, currentFireComp));
                continue;
            }

            if (!_solution.TryGetSolution(uid, currentStain.SolutionName, out var solComp))
            {
                Extinguish((uid, currentFireComp));
                continue;
            }

            var flammability = currentFireComp.Flammability;
            if (flammability <= 0)
            {
                Extinguish((uid, currentFireComp));
                continue;
            }

            if (currentXform.GridUid is not { } gridId)
            {
                Extinguish((uid, currentFireComp));
                continue;
            }

            var wallPos = _transform.GetGridTilePositionOrDefault((uid, currentXform));
            var atmosTilePos = wallPos + currentStain.Direction;

            var tileMix = _atmos.GetTileMixture(gridId, null, atmosTilePos, excite: true);
            var currentOxygen = tileMix?.GetMoles(Gas.Oxygen) ?? 0f;
            var selfOxidizing = currentFireComp.SelfOxidizing;

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

                var burnAmount = selfOxidizing
                    ? 0.2f * flammability
                    : MathF.Min(0.2f * flammability, currentOxygen);
                if (!selfOxidizing)
                    tileMix.AdjustMoles(Gas.Oxygen, -burnAmount);
                tileMix.AdjustMoles(Gas.CarbonDioxide, burnAmount * 0.6f);
                tileMix.AdjustMoles(Gas.WaterVapor, burnAmount * 0.8f);
            }

            if (flammability >= 4)
            {
                var parent = currentXform.ParentUid;
                if (parent.IsValid() && HasComp<DamageableComponent>(parent))
                {
                    var damage = new DamageSpecifier();
                    damage.DamageDict.Add(_structuralDamage, 2.5f * flammability);
                    damage.DamageDict.Add(_heatDamage, 1.5f * flammability);
                    _damageable.TryChangeDamage(parent, damage, ignoreResistances: true);
                }
            }

            if (!currentFireComp.NeedsSpread || !TryComp<MapGridComponent>(gridId, out var grid))
                continue;

            currentFireComp.NeedsSpread = false;
            SpreadFire(uid, gridId, grid, wallPos, atmosTilePos, tileMix, flammability);
        }
    }
}
