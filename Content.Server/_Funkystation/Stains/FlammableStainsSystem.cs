using System.Diagnostics.CodeAnalysis;
using Content.Server._Funkystation.Atmos.Events;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Funkystation.Stains.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Server.Administration.Logs;
using Content.Shared._Funkystation.CCVar;

// Starlight, we've heavily rewritten this to try and make it update less often...

namespace Content.Server._Funkystation.Stains;

public sealed partial class FlammableStainsSystem : EntitySystem
{
    [Dependency] private FlammableSystem _flammable = null!;
    [Dependency] private InventorySystem _inventory = null!;
    [Dependency] private SharedSolutionContainerSystem _solution = null!;
    [Dependency] private IPrototypeManager _prototypeManager = null!;
    [Dependency] private SharedContainerSystem _container = null!;
    [Dependency] private SharedTransformSystem _transform = null!;
    [Dependency] private IConfigurationManager _cfg = null!;
    [Dependency] private IAdminLogManager _adminLogger = default!;

    [Dependency] private EntityQuery<FlammableComponent> _flammableQuery;
    [Dependency] private EntityQuery<InventoryComponent> _inventoryQuery;
    [Dependency] private EntityQuery<StainableComponent> _stainableQuery;
    [Dependency] private EntityQuery<StainBlockerComponent> _blockerQuery;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, ReagentFireCVars.StainFireStackMultiplier, value => _stainStackMultiplier = value, true);

        SubscribeLocalEvent<StainableComponent, MapInitEvent>(OnStainMapInit);
        SubscribeLocalEvent<StainableComponent, ComponentShutdown>(OnStainShutdown);
        SubscribeLocalEvent<StainableComponent, GotEquippedEvent>(OnStainEquipped);
        SubscribeLocalEvent<InventoryComponent, IgnitedEvent>(OnWearerIgnited);
        SubscribeLocalEvent<InventoryComponent, ExtinguishedEvent>(OnWearerExtinguished);
        SubscribeLocalEvent<InventoryComponent, ComponentShutdown>(OnInventoryShutdown);
    }

    [SubscribeLocalEvent]
    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _stainFlammability.Clear();
        _burningWearers.Clear();
    }

    [SubscribeLocalEvent(before: [typeof(FlammableSystem)])]
    private void OnTileFire(Entity<InventoryComponent> ent, ref TileFireEvent args)
    {
        if (_stainFlammability.Count == 0)
            return;

        // Don't keep adding fire stacks every tick if they're already burning...
        if (!_flammableQuery.TryComp(ent.Owner, out var flammable) || flammable.OnFire)
            return;

        var totalStainFlammability = GetTotalStainFlammability(ent.Owner, ent.Comp);
        if (totalStainFlammability <= 0)
            return;

        // Non-linear scaling. lower flammability values are mild, high values ramp up BADLY
        var extraStacks = args.Volume / 100f * (0.5f * MathF.Pow(totalStainFlammability, 1.5f)) * _stainStackMultiplier;
        _flammable.AdjustFireStacks(ent.Owner, extraStacks, flammable);
    }

    [SubscribeLocalEvent]
    private void OnTileExposed(Entity<GridAtmosphereComponent> ent, ref TileExposedEvent args)
    {
        if (_stainFlammability.Count == 0)
            return;

        _tileEntities.Clear();
        _lookup.GetLocalEntitiesIntersecting(ent.Owner, args.Tile, _tileEntities, 0f);

        // A hotspot is local. Inspect this tile instead of every stained item in the round.
        foreach (var wearer in _tileEntities)
        {
            if (!_inventoryQuery.TryComp(wearer, out var inv)
                || !_flammableQuery.TryComp(wearer, out var flammable)
                || flammable.OnFire)
            {
                continue;
            }

            // Must be standing on the exposed tile, not stuffed in a locker on it.
            var wearerXform = Transform(wearer);
            if (wearerXform.GridUid != ent.Owner
                || _container.IsEntityInContainer(wearer)
                || _transform.GetGridTilePositionOrDefault((wearer, wearerXform)) != args.Tile)
                continue;

            var totalStainFlammability = GetTotalStainFlammability(wearer, inv);
            if (totalStainFlammability <= 0)
                continue;

            // Non-linear scaling
            var ignitionTemp = 573.15f - (50f * MathF.Pow(totalStainFlammability, 1.5f));
            if (args.Temperature < ignitionTemp)
                continue;

            var fireStacks = (1f + (0.5f * MathF.Pow(totalStainFlammability, 1.5f))) * _stainStackMultiplier;
            _flammable.AdjustFireStacks(wearer, fireStacks, flammable);

            var igniter = args.SparkSource ?? ent.Owner;
            _flammable.Ignite(wearer, igniter, flammable);

            var reagents = GetFlammableStainsString(wearer, inv);
            _adminLogger.Add(LogType.Flammable, LogImpact.High,
                $"{ToPrettyString(wearer):entity} was ignited by their flammable stains ({reagents}) reacting to a hotspot (Igniter: {ToPrettyString(igniter):entity}).");
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_burningWearers.Count == 0)
            return;

        // Starlight-start: burning is rate-based, so avoid walking the tracked set every tick.
        _updateAccumulator += frameTime;
        if (_updateAccumulator < UpdateInterval)
            return;

        var burnTime = _updateAccumulator;
        _updateAccumulator = 0f;
        // Starlight-end

        _wearerBuffer.Clear();
        _wearerBuffer.AddRange(_burningWearers);

        // Scan each burning inventory once. Blocked slots are also calculated only once per wearer.
        foreach (var wearer in _wearerBuffer)
        {
            if (!_inventoryQuery.TryComp(wearer, out var inv)
                || !_flammableQuery.TryComp(wearer, out var flammable)
                || !flammable.OnFire)
            {
                _burningWearers.Remove(wearer);
                continue;
            }

            var blocked = GetBlockedSlots(wearer, inv);
            var hasFlammableStain = false;

            foreach (var slot in inv.Slots)
            {
                if (!_inventory.TryGetSlotEntity(wearer, slot.Name, out var item, inv)
                    || !_stainFlammability.ContainsKey(item.Value))
                {
                    continue;
                }

                hasFlammableStain = true;

                if ((blocked & slot.SlotFlags) != 0
                    || !_stainableQuery.TryComp(item, out var stain)
                    || !_solution.TryGetSolution(item.Value, stain.SolutionName, out var soln))
                {
                    continue;
                }

                _solution.BurnFlammableReagents(soln.Value, StainBurnRatePerSecond * burnTime);
            }

            if (!hasFlammableStain)
                _burningWearers.Remove(wearer);
        }
    }

    /// <summary>
    /// Gets the entity wearing this item in one of its inventory slots.
    /// </summary>
    private bool TryGetWearer(EntityUid item,
        out EntityUid wearer,
        [NotNullWhen(true)] out InventoryComponent? inv,
        [NotNullWhen(true)] out SlotDefinition? slot)
    {
        wearer = default;
        inv = null;
        slot = null;

        if (!_container.TryGetContainingContainer(item, out var container))
            return false;

        wearer = container.Owner;
        return _inventoryQuery.TryComp(wearer, out inv)
            && _inventory.TryGetSlot(wearer, container.ID, out slot, inv);
    }

    /// <summary>
    /// Slots that are protected from stains by something the wearer has equipped.
    /// </summary>
    private SlotFlags GetBlockedSlots(EntityUid wearer, InventoryComponent inv)
    {
        var blocked = SlotFlags.NONE;
        foreach (var slot in inv.Slots)
        {
            if (_inventory.TryGetSlotEntity(wearer, slot.Name, out var slotEnt, inv)
                && _blockerQuery.TryComp(slotEnt, out var blocker))
            {
                blocked |= blocker.BlockedSlots;
            }
        }

        return blocked;
    }

    private int GetTotalStainFlammability(EntityUid uid, InventoryComponent inv)
    {
        var total = 0;
        var blocked = GetBlockedSlots(uid, inv);
        foreach (var slot in inv.Slots)
        {
            if ((blocked & slot.SlotFlags) != 0)
                continue;

            if (!_inventory.TryGetSlotEntity(uid, slot.Name, out var slotEnt, inv))
                continue;

            if (_stainFlammability.TryGetValue(slotEnt.Value, out var flammability))
                total += flammability;
        }
        return total;
    }

    private string GetFlammableStainsString(EntityUid uid, InventoryComponent inv)
    {
        var names = new HashSet<string>();
        var blocked = GetBlockedSlots(uid, inv);
        foreach (var slot in inv.Slots)
        {
            if ((blocked & slot.SlotFlags) != 0)
                continue;

            if (!_inventory.TryGetSlotEntity(uid, slot.Name, out var slotEnt, inv))
                continue;

            if (_stainableQuery.TryComp(slotEnt, out var stain) &&
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

        return names.Count > 0 ? string.Join(", ", names) : "unknown chemicals";
    }
}
