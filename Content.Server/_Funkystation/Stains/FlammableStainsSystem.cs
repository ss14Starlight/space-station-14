using System.Diagnostics.CodeAnalysis;
using Content.Server._Funkystation.Atmos.Events;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Funkystation.Stains.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Server.Administration.Logs;
using Content.Shared._Funkystation.CCVar;

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

    // Fraction of a stain's flammable reagents consumed per second while on fire
    private const float StainBurnRatePerSecond = 0.2f;
    private float _stainStackMultiplier = 1.0f;

    private readonly HashSet<EntityUid> _flammableStains = [];

    private readonly List<EntityUid> _stainBuffer = [];
    private readonly List<EntityUid> _toPrune = [];
    private readonly HashSet<EntityUid> _checkedWearers = [];

    [Dependency] private EntityQuery<FlammableComponent> _flammableQuery;
    [Dependency] private EntityQuery<InventoryComponent> _inventoryQuery;
    [Dependency] private EntityQuery<StainableComponent> _stainableQuery;
    [Dependency] private EntityQuery<StainBlockerComponent> _blockerQuery;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, ReagentFireCVars.StainFireStackMultiplier, value => _stainStackMultiplier = value, true);
    }

    [SubscribeLocalEvent]
    private void OnRoundRestart(RoundRestartCleanupEvent ev)
        => _flammableStains.Clear();

    /// <summary>
    /// Called when a stainable item gets stained, starts tracking it if the stain can burn.
    /// </summary>
    public void OnStained(EntityUid item, Solution stain)
    {
        if (stain.GetSolutionFlammability(_prototypeManager) > 0)
            _flammableStains.Add(item);
    }

    [SubscribeLocalEvent(before: [typeof(FlammableSystem)])]
    private void OnTileFire(Entity<InventoryComponent> ent, ref TileFireEvent args)
    {
        if (_flammableStains.Count == 0)
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
        if (_flammableStains.Count == 0)
            return;

        _checkedWearers.Clear();
        _stainBuffer.Clear();
        _stainBuffer.AddRange(_flammableStains);

        // Instead of looking up everything on the tile, check where the few flammable stains are.
        foreach (var item in _stainBuffer)
        {
            if (!IsStillFlammable(item))
            {
                _toPrune.Add(item);
                continue;
            }

            if (!TryGetWearer(item, out var wearer, out var inv, out _) || !_checkedWearers.Add(wearer))
                continue;

            if (!_flammableQuery.TryComp(wearer, out var flammable) || flammable.OnFire)
                continue;

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

        PruneStains();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_flammableStains.Count == 0)
            return;

        _stainBuffer.Clear();
        _stainBuffer.AddRange(_flammableStains);

        // Actively burn off stains while the wearer is on fire, same as puddles.
        foreach (var item in _stainBuffer)
        {
            if (TerminatingOrDeleted(item))
            {
                _toPrune.Add(item);
                continue;
            }

            if (!TryGetWearer(item, out var wearer, out var inv, out var slot))
                continue;

            if (!_flammableQuery.TryComp(wearer, out var flammable) || !flammable.OnFire)
                continue;

            if ((GetBlockedSlots(wearer, inv) & slot.SlotFlags) != 0)
                continue;

            if (!_stainableQuery.TryComp(item, out var stain)
                || !_solution.TryGetSolution(item, stain.SolutionName, out var soln, out var solution)
                || solution.GetSolutionFlammability(_prototypeManager) <= 0)
            {
                _toPrune.Add(item);
                continue;
            }

            _solution.BurnFlammableReagents(soln.Value, StainBurnRatePerSecond * frameTime);
        }

        PruneStains();
    }

    private void PruneStains()
    {
        foreach (var item in _toPrune)
        {
            _flammableStains.Remove(item);
        }

        _toPrune.Clear();
    }

    private bool IsStillFlammable(EntityUid item)
        => !TerminatingOrDeleted(item)
            && _stainableQuery.TryComp(item, out var stain)
            && _solution.TryGetSolution(item, stain.SolutionName, out _, out var solution)
            && solution.GetSolutionFlammability(_prototypeManager) > 0;

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

            if (_stainableQuery.TryComp(slotEnt, out var stain) &&
                _solution.TryGetSolution(slotEnt.Value, stain.SolutionName, out _, out var solution))
            {
                total += solution.GetSolutionFlammability(_prototypeManager);
            }
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
