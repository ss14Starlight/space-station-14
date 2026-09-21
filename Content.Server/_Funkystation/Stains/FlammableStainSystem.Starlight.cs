using Content.Shared._Funkystation.Stains.Components;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Server._Funkystation.Stains;

public sealed partial class FlammableStainsSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = null!;

    // Fraction of a stain's flammable reagents consumed per second while on fire
    private const float StainBurnRatePerSecond = 0.2f;

    private const float UpdateInterval = 0.5f;
    private float _updateAccumulator;

    private float _stainStackMultiplier = 1.0f;

    // Updated by solution events. Hotspot exposure can read this cache instead of walking reagent prototypes.
    private readonly Dictionary<EntityUid, int> _stainFlammability = [];
    // Only burning wearers need periodic stain consumption.
    private readonly HashSet<EntityUid> _burningWearers = [];
    private readonly List<EntityUid> _wearerBuffer = [];
    private readonly HashSet<EntityUid> _tileEntities = [];

    /// <summary>
    /// Refreshes cached flammability after a stain solution changes.
    /// </summary>
    public void OnStainSolutionChanged(EntityUid item, Solution stain) => UpdateStainFlammability(item, stain);

    private void OnStainMapInit(Entity<StainableComponent> ent, ref MapInitEvent args)
    {
        if (_solution.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out _, out var solution))
            UpdateStainFlammability(ent.Owner, solution);
    }

    private void OnStainShutdown(Entity<StainableComponent> ent, ref ComponentShutdown args) => _stainFlammability.Remove(ent.Owner);

    private void OnStainEquipped(Entity<StainableComponent> ent, ref GotEquippedEvent args)
    {
        if (_stainFlammability.ContainsKey(ent.Owner)
            && _flammableQuery.TryComp(args.EquipTarget, out var flammable)
            && flammable.OnFire)
        {
            _burningWearers.Add(args.EquipTarget);
        }
    }

    private void OnWearerIgnited(Entity<InventoryComponent> ent, ref IgnitedEvent args) => _burningWearers.Add(ent.Owner);

    private void OnWearerExtinguished(Entity<InventoryComponent> ent, ref ExtinguishedEvent args) => _burningWearers.Remove(ent.Owner);

    private void OnInventoryShutdown(Entity<InventoryComponent> ent, ref ComponentShutdown args) => _burningWearers.Remove(ent.Owner);

    private void UpdateStainFlammability(EntityUid item, Solution solution)
    {
        var flammability = solution.GetSolutionFlammability(_prototypeManager);
        if (flammability <= 0)
        {
            _stainFlammability.Remove(item);
            return;
        }

        _stainFlammability[item] = flammability;

        if (TryGetWearer(item, out var wearer, out _, out _)
            && _flammableQuery.TryComp(wearer, out var wearerFlammable)
            && wearerFlammable.OnFire)
        {
            _burningWearers.Add(wearer);
        }
    }
}
