using Content.Server._Funkystation.Atmos.Events;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Funkystation.Stains.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Server._Funkystation.Stains
{
    public sealed class FlammableStainsSystem : EntitySystem
    {
        [Dependency] private readonly FlammableSystem _flammable = null!;
        [Dependency] private readonly InventorySystem _inventory = null!;
        [Dependency] private readonly SharedSolutionContainerSystem _solution = null!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = null!;
        [Dependency] private readonly EntityLookupSystem _lookup = null!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<InventoryComponent, TileFireEvent>(OnTileFire, before: [typeof(FlammableSystem)]);
            SubscribeLocalEvent<GridAtmosphereComponent, TileExposedEvent>(OnTileExposed);
        }

        private void OnTileFire(EntityUid uid, InventoryComponent component, ref TileFireEvent args)
        {
            var totalStainFlammability = GetTotalStainFlammability(uid, component);
            if (totalStainFlammability <= 0)
                return;

            if (TryComp<FlammableComponent>(uid, out var flammable))
            {
                var extraStacks = (args.Volume / 100f) * (0.5f * totalStainFlammability);
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
                if (!TryComp<InventoryComponent>(ent, out var inv))
                    continue;

                var totalStainFlammability = GetTotalStainFlammability(ent, inv);
                if (totalStainFlammability <= 0)
                    continue;

                var ignitionTemp = 573.15f - (50f * totalStainFlammability);
                if (args.Temperature >= ignitionTemp)
                {
                    if (TryComp<FlammableComponent>(ent, out var flammable))
                    {
                        var fireStacks = 1f + (0.5f * totalStainFlammability);
                        _flammable.AdjustFireStacks(ent, fireStacks, flammable);
                        _flammable.Ignite(ent, ent, flammable);
                    }
                }
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
    }
}
