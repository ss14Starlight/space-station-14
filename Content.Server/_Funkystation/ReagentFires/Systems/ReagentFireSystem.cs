using Content.Server._Funkystation.Atmos.Events;
using Content.Server._Funkystation.ReagentFires.Components;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Decals;
using Content.Shared._Funkystation.ReagentFires;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Fluids.Components;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Funkystation.ReagentFires.Systems
{
    public sealed class ReagentFireSystem : EntitySystem
    {
        [Dependency] private readonly AtmosphereSystem _atmos = null!;
        [Dependency] private readonly SharedTransformSystem _transform = null!;
        [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = null!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = null!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = null!;
        [Dependency] private readonly EntityLookupSystem _lookup = null!;
        [Dependency] private readonly SharedAudioSystem _audio = null!;
        [Dependency] private readonly SharedPointLightSystem _light = null!;
        [Dependency] private readonly DecalSystem _decalSystem = null!;
        [Dependency] private readonly IRobustRandom _random = null!;
        [Dependency] private readonly DamageableSystem _damageable = null!;

        private readonly List<EntityUid> _toExtinguish = new();
        private readonly string[] _burntDecals = ["burnt1", "burnt2", "burnt3", "burnt4"];

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SolutionComponent, SolutionChangedEvent>(OnSolutionChanged);
            SubscribeLocalEvent<TransformComponent, TileExposedEvent>(OnTileExposed);
            SubscribeLocalEvent<PuddleComponent, TileFireEvent>(OnPuddleTileFire);
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

        private void OnSolutionChanged(EntityUid uid, SolutionComponent component, ref SolutionChangedEvent args)
        {
            if (!TryComp<ContainedSolutionComponent>(uid, out var relation))
                return;

            var containerUid = relation.Container;
            if (!TryComp<PuddleComponent>(containerUid, out _))
                return;

            var solution = component.Solution;
            var flammability = solution.GetSolutionFlammability(_prototypeManager);
            var selfOxidizing = solution.IsSolutionSelfOxidizing(_prototypeManager);

            if (flammability <= 0)
            {
                if (HasComp<ReagentPuddleFireComponent>(containerUid))
                {
                    Extinguish(containerUid);
                }
                return;
            }

            var fireComp = EnsureComp<ReagentPuddleFireComponent>(containerUid);
            fireComp.Flammability = flammability;
            fireComp.SelfOxidizing = selfOxidizing;

            if (flammability > 10)
                fireComp.FireState = 6;
            else if (flammability > 5)
                fireComp.FireState = 5;
            else
                fireComp.FireState = 4;

            if (fireComp.OnFire)
            {
                var fireColor = GetFireColor(fireComp.Flammability);
                if (fireComp.FireEffectEntity != null)
                {
                    _appearance.SetData(fireComp.FireEffectEntity.Value, ReagentPuddleFireVisuals.FireState, fireComp.FireState);
                    _appearance.SetData(fireComp.FireEffectEntity.Value, ReagentPuddleFireVisuals.FireColor, fireColor);
                }

                if (TryComp<PointLightComponent>(containerUid, out var light))
                {
                    _light.SetRadius(containerUid, MathF.Max(2f, fireComp.FireState - 1f), light);
                    _light.SetColor(containerUid, fireColor, light);
                }
            }
        }

        private void OnTileExposed(EntityUid gridUid, TransformComponent component, ref TileExposedEvent args)
        {
            var tilePos = args.Tile;
            var puddles = GetPuddlesOnTile(gridUid, tilePos);

            foreach (var puddle in puddles)
            {
                if (!TryComp<ReagentPuddleFireComponent>(puddle, out var fireComp) || fireComp.OnFire)
                    continue;

                var ignitionTemp = 573.15f - (50f * fireComp.Flammability);
                if (args.Temperature >= ignitionTemp)
                {
                    Ignite(puddle, fireComp);
                    _atmos.GetTileMixture(gridUid, null, tilePos, excite: true);
                }
            }
        }

        private void OnPuddleTileFire(EntityUid uid, PuddleComponent component, ref TileFireEvent args)
        {
            if (TryComp<ReagentPuddleFireComponent>(uid, out var fireComp) && !fireComp.OnFire)
            {
                var ignitionTemp = 573.15f - (50f * fireComp.Flammability);
                if (args.Temperature >= ignitionTemp)
                {
                    Ignite(uid, fireComp);
                }
            }
        }

        private IEnumerable<EntityUid> GetPuddlesOnTile(EntityUid gridUid, Vector2i tilePos)
        {
            var results = new List<EntityUid>();
            var entities = new HashSet<EntityUid>();
            _lookup.GetLocalEntitiesIntersecting(gridUid, tilePos, entities, 0f);
            foreach (var ent in entities)
            {
                if (HasComp<PuddleComponent>(ent))
                    results.Add(ent);
            }
            return results;
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

        private void Ignite(EntityUid uid, ReagentPuddleFireComponent? fireComp = null)
        {
            if (!Resolve(uid, ref fireComp))
                return;

            if (fireComp.OnFire)
                return;

            fireComp.OnFire = true;

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

            if (fireComp.FireEffectEntity != null)
            {
                _appearance.SetData(fireComp.FireEffectEntity.Value, ReagentPuddleFireVisuals.FireState, fireComp.FireState);
                _appearance.SetData(fireComp.FireEffectEntity.Value, ReagentPuddleFireVisuals.FireColor, fireColor);
            }
        }

        private void Extinguish(EntityUid uid)
        {
            if (!TryComp<ReagentPuddleFireComponent>(uid, out var fireComp))
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

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            _toExtinguish.Clear();

            var activeQuery = EntityQueryEnumerator<ReagentPuddleFireComponent, PuddleComponent, TransformComponent>();
            while (activeQuery.MoveNext(out var uid, out var fireComp, out var puddle, out var xform))
            {
                if (!fireComp.OnFire)
                {
                    if (fireComp.Flammability <= 0)
                        continue;

                    var gridId = xform.GridUid;
                    if (gridId != null)
                    {
                        var ambientPos = _transform.GetGridTilePositionOrDefault((uid, xform));
                        var ambientMix = _atmos.GetTileMixture(gridId.Value, null, ambientPos, excite: false);
                        if (ambientMix != null)
                        {
                            var autoIgnitionTemp = 773.15f - (50f * fireComp.Flammability);
                            var ambientOxygen = ambientMix.GetMoles(Gas.Oxygen);

                            if (ambientMix.Temperature >= autoIgnitionTemp && (fireComp.SelfOxidizing || ambientOxygen > 0.1f))
                            {
                                Ignite(uid, fireComp);
                                _atmos.GetTileMixture(gridId.Value, null, ambientPos, excite: true);
                            }
                        }
                    }
                    continue;
                }

                fireComp.Accumulator += frameTime;
                if (fireComp.Accumulator < 1f)
                    continue;

                fireComp.Accumulator -= 1f;

                var gridUid = xform.GridUid;
                if (gridUid == null)
                {
                    _toExtinguish.Add(uid);
                    continue;
                }

                var tilePos = _transform.GetGridTilePositionOrDefault((uid, xform));
                var tileMix = _atmos.GetTileMixture(gridUid.Value, null, tilePos, excite: true);

                var oxygenMoles = tileMix?.GetMoles(Gas.Oxygen) ?? 0f;
                if (!fireComp.SelfOxidizing && oxygenMoles <= 0.1f)
                {
                    _toExtinguish.Add(uid);
                    continue;
                }

                if (!_solutionContainerSystem.ResolveSolution(uid, puddle.SolutionName, ref puddle.Solution, out var solution))
                {
                    _toExtinguish.Add(uid);
                    continue;
                }

                var burnFraction = 0.05f / MathF.Pow(MathF.Max(1f, fireComp.Flammability), 3f);
                _solutionContainerSystem.BurnFlammableReagents(puddle.Solution.Value, burnFraction);

                var flammability = solution.GetSolutionFlammability(_prototypeManager);
                var selfOxidizing = solution.IsSolutionSelfOxidizing(_prototypeManager);

                if (flammability <= 0)
                {
                    _toExtinguish.Add(uid);
                    continue;
                }

                fireComp.SelfOxidizing = selfOxidizing;

                if (flammability > 10)
                    fireComp.FireState = 6;
                else if (flammability > 5)
                    fireComp.FireState = 5;
                else
                    fireComp.FireState = 4;

                var fireColor = GetFireColor(fireComp.Flammability);

                if (fireComp.FireEffectEntity != null)
                {
                    _appearance.SetData(fireComp.FireEffectEntity.Value, ReagentPuddleFireVisuals.FireState, fireComp.FireState);
                    _appearance.SetData(fireComp.FireEffectEntity.Value, ReagentPuddleFireVisuals.FireColor, fireColor);
                }

                if (TryComp<PointLightComponent>(uid, out var light))
                {
                    _light.SetRadius(uid, MathF.Max(2f, fireComp.FireState - 1f), light);
                    _light.SetColor(uid, fireColor, light);
                }

                if (tileMix != null)
                {
                    var maxTemp = Atmospherics.T0C + 100f * MathF.Pow(flammability, 1.5f);
                    if (tileMix.Temperature < maxTemp)
                    {
                        var heatRate = 10f * flammability;
                        tileMix.Temperature = MathF.Min(tileMix.Temperature + heatRate, maxTemp);
                    }

                    if (!fireComp.SelfOxidizing)
                    {
                        var burnAmount = MathF.Min(0.2f * flammability, oxygenMoles);
                        tileMix.AdjustMoles(Gas.Oxygen, -burnAmount);
                        tileMix.AdjustMoles(Gas.CarbonDioxide, burnAmount * 0.6f);
                        tileMix.AdjustMoles(Gas.WaterVapor, burnAmount * 0.8f);
                    }
                    else
                    {
                        var burnAmount = 0.2f * flammability;
                        tileMix.AdjustMoles(Gas.CarbonDioxide, burnAmount * 0.6f);
                        tileMix.AdjustMoles(Gas.WaterVapor, burnAmount * 0.8f);
                    }
                }

                var tileDecals = _decalSystem.GetDecalsInRange(gridUid.Value, tilePos);
                var tileBurntDecals = 0;
                foreach (var set in tileDecals)
                {
                    if (Array.IndexOf(_burntDecals, set.Decal.Id) == -1)
                        continue;
                    tileBurntDecals++;
                    if (tileBurntDecals > 4)
                        break;
                }

                if (tileBurntDecals < 4 && _random.Prob(0.25f))
                {
                    _decalSystem.TryAddDecal(_burntDecals[_random.Next(_burntDecals.Length)],
                        new EntityCoordinates(gridUid.Value, tilePos),
                        out _,
                        cleanable: true);
                }

                var directions = new[] { new Vector2i(0, 1), new Vector2i(0, -1), new Vector2i(1, 0), new Vector2i(-1, 0) };
                foreach (var offset in directions)
                {
                    var adjacentPos = tilePos + offset;
                    var adjMix = _atmos.GetTileMixture(gridUid.Value, null, adjacentPos, excite: true);

                    var isBlocked = false;
                    var adjacentEntities = new HashSet<EntityUid>();
                    _lookup.GetLocalEntitiesIntersecting(gridUid.Value, adjacentPos, adjacentEntities, 0f);
                    foreach (var ent in adjacentEntities)
                    {
                        if (TryComp<AirtightComponent>(ent, out var airtight) && airtight.AirBlocked)
                        {
                            isBlocked = true;
                            break;
                        }
                    }

                    if (!isBlocked && adjMix != null && tileMix is { Temperature: > Atmospherics.FireMinimumTemperatureToSpread })
                    {
                        var radiatedTemp = tileMix.Temperature * Atmospherics.FireSpreadRadiosityScale;
                        if (adjMix.Temperature < radiatedTemp)
                        {
                            adjMix.Temperature = MathF.Max(adjMix.Temperature, radiatedTemp);
                        }
                    }

                    foreach (var adjPuddle in GetPuddlesOnTile(gridUid.Value, adjacentPos))
                    {
                        if (TryComp<ReagentPuddleFireComponent>(adjPuddle, out var adjFireComp) && !adjFireComp.OnFire)
                        {
                            Ignite(adjPuddle, adjFireComp);
                        }
                    }
                }

                var standingEntities = new HashSet<EntityUid>();
                _lookup.GetLocalEntitiesIntersecting(gridUid.Value, tilePos, standingEntities, 0f);

                var structuralProto = _prototypeManager.Index<DamageTypePrototype>("Structural");
                var heatProto = _prototypeManager.Index<DamageTypePrototype>("Heat");

                var structuralDamage = new DamageSpecifier(structuralProto, 2f * flammability);
                var heatDamage = new DamageSpecifier(heatProto, 2f * flammability);

                var totalDamage = structuralDamage + heatDamage;

                var fireVolume = 50f * flammability;
                var fireEvent = new TileFireEvent(tileMix?.Temperature ?? (Atmospherics.T0C + 50f * flammability), fireVolume);

                var xformQuery = GetEntityQuery<TransformComponent>();
                foreach (var ent in standingEntities)
                {
                    if (ent == uid || Deleted(ent))
                        continue;

                    if (!xformQuery.TryGetComponent(ent, out var entXform))
                        continue;

                    if (_transform.GetGridTilePositionOrDefault((ent, entXform)) != tilePos)
                        continue;

                    if (HasComp<DamageableComponent>(ent))
                    {
                        var ignoreResistances = !HasComp<MobStateComponent>(ent);
                        _damageable.TryChangeDamage(ent, totalDamage, ignoreResistances: ignoreResistances);
                    }

                    if (Deleted(ent))
                        continue;

                    RaiseLocalEvent(ent, ref fireEvent);
                }
            }

            foreach (var uid in _toExtinguish)
            {
                Extinguish(uid);
            }
        }
    }
}
