using Content.Server._Funkystation.Atmos.Events;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Funkystation.Stains.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.Inventory;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Content.Server.Administration.Logs;
using Content.Shared._Funkystation.CCVar;

namespace Content.Server._Funkystation.Stains
{
    public sealed partial class FlammableStainsSystem : EntitySystem
    {
        [Dependency] private FlammableSystem _flammable = null!;
        [Dependency] private InventorySystem _inventory = null!;
        [Dependency] private SharedSolutionContainerSystem _solution = null!;
        [Dependency] private IPrototypeManager _prototypeManager = null!;
        [Dependency] private EntityLookupSystem _lookup = null!;
        [Dependency] private IConfigurationManager _cfg = null!;
        [Dependency] private IAdminLogManager _adminLogger = default!;

        // Fraction of a stain's flammable reagents consumed per second while on fire
        private const float StainBurnRatePerSecond = 0.2f;
        private float _stainStackMultiplier = 1.0f;

        public override void Initialize()
        {
            base.Initialize();
            Subs.CVar(_cfg, ReagentFireCVars.StainFireStackMultiplier, value => _stainStackMultiplier = value, true);
            SubscribeLocalEvent<InventoryComponent, TileFireEvent>(OnTileFire, before: [typeof(FlammableSystem)]);
            SubscribeLocalEvent<GridAtmosphereComponent, TileExposedEvent>(OnTileExposed);
        }

        private void OnTileFire(EntityUid uid, InventoryComponent component, ref TileFireEvent args)
        {
            var totalStainFlammability = GetTotalStainFlammability(uid, component);
            if (totalStainFlammability <= 0)
                return;

            // Don't keep adding fire stacks every tick if they're already burning...
            if (TryComp<FlammableComponent>(uid, out var flammable) && !flammable.OnFire)
            {
                // Non-linear scaling. lower flammability values are mild, high values ramp up BADLY
                var extraStacks = (args.Volume / 100f) * (0.5f * MathF.Pow(totalStainFlammability, 1.5f)) * _stainStackMultiplier;
                _flammable.AdjustFireStacks(uid, extraStacks, flammable);
            }
        }

        private void OnTileExposed(EntityUid gridUid, GridAtmosphereComponent component, ref TileExposedEvent args)
        {
            var tilePos = args.Tile;
            var entities = new HashSet<EntityUid>();
            _lookup.GetLocalEntitiesIntersecting(gridUid, tilePos, entities, 0f);

            foreach (var ent in entities)
            {
                if (!TryComp<InventoryComponent>(ent, out var inv) || !TryComp<FlammableComponent>(ent, out var flammable))
                    continue;

                if (flammable.OnFire)
                    continue;

                var totalStainFlammability = GetTotalStainFlammability(ent, inv);
                if (totalStainFlammability <= 0)
                    continue;

                // Non-linear scaling
                var ignitionTemp = 573.15f - (50f * MathF.Pow(totalStainFlammability, 1.5f));
                if (args.Temperature >= ignitionTemp)
                {
                    var fireStacks = (1f + (0.5f * MathF.Pow(totalStainFlammability, 1.5f))) * _stainStackMultiplier;
                    _flammable.AdjustFireStacks(ent, fireStacks, flammable);

                    var igniter = args.SparkSource ?? gridUid;
                    _flammable.Ignite(ent, igniter, flammable);

                    var reagents = GetFlammableStainsString(ent, inv);
                    _adminLogger.Add(LogType.Flammable, LogImpact.High,
                        $"{ToPrettyString(ent):entity} was ignited by their flammable stains ({reagents}) reacting to a hotspot (Igniter: {ToPrettyString(igniter):entity}).");
                }
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            // Actively burn off stains while the wearer is on fire, same as puddles.
            var query = EntityQueryEnumerator<FlammableComponent, InventoryComponent>();
            while (query.MoveNext(out var uid, out var flammable, out var inv))
            {
                if (!flammable.OnFire)
                    continue;

                BurnStains(uid, inv, frameTime);
            }
        }

        private void BurnStains(EntityUid uid, InventoryComponent inv, float frameTime)
        {
            foreach (var slot in inv.Slots)
            {
                if (!_inventory.TryGetSlotEntity(uid, slot.Name, out var slotEnt, inv))
                    continue;

                if (IsSlotStainBlocked(uid, slot, inv))
                    continue;

                if (!TryComp<StainableComponent>(slotEnt, out var stain) ||
                    !_solution.TryGetSolution(slotEnt.Value, stain.SolutionName, out var soln, out var solution))
                    continue;

                if (solution.GetSolutionFlammability(_prototypeManager) <= 0)
                    continue;

                _solution.BurnFlammableReagents(soln.Value, StainBurnRatePerSecond * frameTime);
            }
        }

        private int GetTotalStainFlammability(EntityUid uid, InventoryComponent inv)
        {
            var total = 0;
            foreach (var slot in inv.Slots)
            {
                if (!_inventory.TryGetSlotEntity(uid, slot.Name, out var slotEnt, inv))
                    continue;

                if (IsSlotStainBlocked(uid, slot, inv))
                    continue;

                if (TryComp<StainableComponent>(slotEnt, out var stain) &&
                    _solution.TryGetSolution(slotEnt.Value, stain.SolutionName, out _, out var solution))
                {
                    total += solution.GetSolutionFlammability(_prototypeManager);
                }
            }
            return total;
        }

        private bool IsSlotStainBlocked(EntityUid wearer, SlotDefinition slotDef, InventoryComponent inv)
        {
            foreach (var slot in inv.Slots)
            {
                if (!_inventory.TryGetSlotEntity(wearer, slot.Name, out var slotEnt, inv))
                    continue;

                if (TryComp<StainBlockerComponent>(slotEnt, out var blocker))
                {
                    if ((blocker.BlockedSlots & slotDef.SlotFlags) != 0)
                        return true;
                }
            }
            return false;
        }

        private string GetFlammableStainsString(EntityUid uid, InventoryComponent inv)
        {
            var names = new List<string>();
            foreach (var slot in inv.Slots)
            {
                if (!_inventory.TryGetSlotEntity(uid, slot.Name, out var slotEnt, inv))
                    continue;

                if (IsSlotStainBlocked(uid, slot, inv))
                    continue;

                if (TryComp<StainableComponent>(slotEnt, out var stain) &&
                    _solution.TryGetSolution(slotEnt.Value, stain.SolutionName, out _, out var solution))
                {
                    foreach (var (reagentId, _) in solution.Contents)
                    {
                        if (_prototypeManager.TryIndex<ReagentPrototype>(reagentId.Prototype, out var proto) && proto.Flammability > 0)
                        {
                            names.Add(proto.LocalizedName);
                        }
                    }
                }
            }

            return names.Count > 0 ? string.Join(", ", new HashSet<string>(names)) : "unknown chemicals";
        }
    }
}
