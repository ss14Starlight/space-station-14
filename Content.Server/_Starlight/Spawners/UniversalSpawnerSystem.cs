using System.Linq;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Holiday;
using Content.Server.Mind;
using Content.Shared.Ghost;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind.Components;
using Content.Shared._Starlight.UniversalSpawner;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.UniversalSpawner;

/// <summary>
/// System that handles universal spawner logic and UI interactions
/// </summary>
public sealed class UniversalSpawnerSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly HolidaySystem _holidaySystem = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _timeTriggeredSpawners = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniversalSpawnerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<UniversalSpawnerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<UniversalSpawnerComponent, UniversalSpawnerUpdateEntriesMessage>(OnUpdateEntries);
        SubscribeLocalEvent<UniversalSpawnerComponent, UniversalSpawnerUpdateSettingsMessage>(OnUpdateSettings);
        SubscribeLocalEvent<UniversalSpawnerComponent, UniversalSpawnerTriggerMessage>(OnTriggerSpawn);
        SubscribeLocalEvent<UniversalSpawnerComponent, UniversalSpawnerResetMessage>(OnReset);
        SubscribeLocalEvent<UniversalSpawnerComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<GameRuleStartedEvent>(OnGameRuleStarted);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Check time-based triggers
        var query = EntityQueryEnumerator<UniversalSpawnerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.HasSpawned)
                continue;

            // Time-based trigger
            if (comp.TriggerType == SpawnerTriggerType.TimeIntoRound)
            {
                if (_timeTriggeredSpawners.Contains(uid))
                    continue;

                var roundTime = _timing.CurTime - _gameTicker.RoundStartTimeSpan;
                if (roundTime.TotalSeconds >= comp.TriggerTimeSeconds)
                {
                    TrySpawn(uid, comp);
                    _timeTriggeredSpawners.Add(uid);
                }
            }
            
            // Proximity trigger
            if (comp.TriggerType == SpawnerTriggerType.Proximity)
            {
                if (!TryComp(uid, out TransformComponent? xform))
                    continue;
                    
                var spawnerPos = _transform.GetWorldPosition(xform);
                var range = comp.ProximityRange;
                
                // Check for any entity with Mind component (but not Ghost) within range
                var mindQuery = EntityQueryEnumerator<MindContainerComponent, TransformComponent>();
                while (mindQuery.MoveNext(out var targetUid, out _, out var targetXform))
                {
                    // Skip ghosts
                    if (HasComp<GhostComponent>(targetUid))
                        continue;
                        
                    var targetPos = _transform.GetWorldPosition(targetXform);
                    var distance = (targetPos - spawnerPos).Length();
                    
                    if (distance <= range)
                    {
                        TrySpawn(uid, comp);
                        break;
                    }
                }
            }
        }
    }

    private void OnStartup(EntityUid uid, UniversalSpawnerComponent component, ComponentStartup args) =>
        _uiSystem.TryGetOpenUi(uid, UniversalSpawnerUiKey.Key, out _);

    private void OnMapInit(EntityUid uid, UniversalSpawnerComponent component, MapInitEvent args)
    {
        if (component.TriggerType == SpawnerTriggerType.MapInit && !component.HasSpawned)
        {
            TrySpawn(uid, component);
        }
    }

    private void OnGameRuleStarted(ref GameRuleStartedEvent ev)
    {
        if (string.IsNullOrEmpty(ev.RuleId))
            return;

        // Trigger all spawners waiting for this gamerule
        var query = EntityQueryEnumerator<UniversalSpawnerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.HasSpawned)
                continue;

            if (comp.TriggerType != SpawnerTriggerType.Gamerule)
                continue;

            if (string.IsNullOrEmpty(comp.TriggerGameRule))
                continue;

            if (comp.TriggerGameRule == ev.RuleId)
            {
                TrySpawn(uid, comp);
            }
        }
    }

    private void OnUIOpened(EntityUid uid, UniversalSpawnerComponent component, BoundUIOpenedEvent args) =>
        UpdateUserInterface(uid, component);

    private void OnUpdateEntries(EntityUid uid, UniversalSpawnerComponent component, UniversalSpawnerUpdateEntriesMessage args)
    {
        component.Entries = args.Entries;
        Dirty(uid, component);
        UpdateUserInterface(uid, component);
    }

    private void OnUpdateSettings(EntityUid uid, UniversalSpawnerComponent component, UniversalSpawnerUpdateSettingsMessage args)
    {
        component.MaxSpawns = args.MaxSpawns;
        component.Offset = args.Offset;
        component.DeleteAfterSpawn = args.DeleteAfterSpawn;
        component.SpawnChance = args.SpawnChance;
        component.MinSpawns = args.MinSpawns;
        component.MinRolls = args.MinRolls;
        component.MaxRolls = args.MaxRolls;
        component.TriggerType = args.TriggerType;
        component.TriggerTimeSeconds = args.TriggerTimeSeconds;
        component.TriggerGameRule = args.TriggerGameRule;
        component.ProximityRange = args.ProximityRange;
        Dirty(uid, component);
        UpdateUserInterface(uid, component);
    }

    private void OnTriggerSpawn(EntityUid uid, UniversalSpawnerComponent component, UniversalSpawnerTriggerMessage args)
    {
        TrySpawn(uid, component, isManual: true);
        UpdateUserInterface(uid, component);
    }

    private void OnReset(EntityUid uid, UniversalSpawnerComponent component, UniversalSpawnerResetMessage args)
    {
        component.HasSpawned = false;
        Dirty(uid, component);
        UpdateUserInterface(uid, component);
    }

    /// <summary>
    /// Attempts to spawn entities based on spawner configuration.
    /// </summary>
    /// <param name="isManual">If true, doesn't set HasSpawned flag</param>
    public bool TrySpawn(EntityUid uid, UniversalSpawnerComponent component, bool isManual = false)
    {
        if (component.HasSpawned)
            return false;

        // Check spawn chance
        if (component.SpawnChance < 1.0f && !_random.Prob(component.SpawnChance))
        {
            component.HasSpawned = true;
            Dirty(uid, component);
            if (component.DeleteAfterSpawn)
                QueueDel(uid);
            return false;
        }

        // Filter entries by holiday and enabled status
        var validEntries = new List<(SpawnEntry entry, float weight)>();
        foreach (var entry in component.Entries)
        {
            if (!entry.Enabled || entry.Weight <= 0)
                continue;

            // Check holiday condition
            if (!string.IsNullOrEmpty(entry.Holiday))
            {
                // "*" means any holiday must be active
                if (entry.Holiday == "*")
                {
                    // Check if any holiday is currently active
                    var hasActiveHoliday = false;
                    foreach (var holiday in _prototypeManager.EnumeratePrototypes<HolidayPrototype>())
                    {
                        if (_holidaySystem.IsCurrentlyHoliday(holiday.ID))
                        {
                            hasActiveHoliday = true;
                            break;
                        }
                    }
                    if (!hasActiveHoliday)
                        continue;
                }
                else
                {
                    if (!_holidaySystem.IsCurrentlyHoliday(entry.Holiday))
                        continue;
                }
            }

            // Validate prototype exists (check if at least one ID in comma-separated list is valid)
            var prototypeIds = entry.PrototypeId.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var hasValidPrototype = false;
            foreach (var protoId in prototypeIds)
            {
                if (!string.IsNullOrWhiteSpace(protoId) && _prototypeManager.HasIndex<EntityPrototype>(protoId))
                {
                    hasValidPrototype = true;
                    break;
                }
            }
            
            if (!hasValidPrototype)
                continue;

            validEntries.Add((entry, entry.Weight));
        }

        if (validEntries.Count == 0)
        {
            component.HasSpawned = true;
            Dirty(uid, component);
            if (component.DeleteAfterSpawn)
                QueueDel(uid);
            return false;
        }

        var spawnerXform = Transform(uid);
        var spawnerPos = spawnerXform.Coordinates;

        var rollCount = component.MinRolls;
        if (component.MaxRolls > component.MinRolls)
        {
            rollCount = _random.Next(component.MinRolls, component.MaxRolls + 1);
        }
        rollCount = Math.Min(rollCount, validEntries.Count);
        if (rollCount <= 0)
            rollCount = 1;

        var selectedEntries = new List<SpawnEntry>();
        var remainingEntries = new List<(SpawnEntry entry, float weight)>(validEntries);

        // Perform rolls
        for (var i = 0; i < rollCount; i++)
        {
            if (remainingEntries.Count == 0)
                break;

            var picked = PickWeightedEntry(remainingEntries);
            if (picked == null)
                break;

            selectedEntries.Add(picked);

            if (!picked.AllowMultiple)
            {
                remainingEntries.RemoveAll(e => e.entry == picked);
            }
        }

        // Now spawn from selected entries
        foreach (var entry in selectedEntries)
        {
            // Calculate how many to spawn
            var spawnCount = component.MinSpawns;
            if (component.MaxSpawns > component.MinSpawns)
            {
                spawnCount = _random.Next(component.MinSpawns, component.MaxSpawns + 1);
            }
            else if (component.MaxSpawns > 0)
            {
                spawnCount = Math.Min(component.MinSpawns, component.MaxSpawns);
            }

            // Spawn the quantity specified in the entry
            for (var q = 0; q < entry.Quantity; q++)
            {
                for (var s = 0; s < spawnCount; s++)
                {
                    // Calculate spawn position with combined offsets
                    var spawnPos = spawnerPos;
                    var totalOffset = component.Offset + entry.Offset;
                    if (totalOffset > 0)
                    {
                        var offsetX = _random.NextFloat(-totalOffset, totalOffset);
                        var offsetY = _random.NextFloat(-totalOffset, totalOffset);
                        spawnPos = spawnerPos.Offset(new Vector2(offsetX, offsetY));
                    }

                    var prototypeIds = entry.PrototypeId.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var protoId in prototypeIds)
                    {
                        // Skip invalid prototype IDs instead of failing
                        if (!string.IsNullOrWhiteSpace(protoId) && _prototypeManager.HasIndex<EntityPrototype>(protoId))
                        {
                            Spawn(protoId, spawnPos);
                        }
                    }
                }
            }
        }

        // Manual spawns don't count toward HasSpawned
        if (!isManual)
        {
            component.HasSpawned = true;
        }
        Dirty(uid, component);

        if (component.DeleteAfterSpawn)
            QueueDel(uid);

        return true;
    }

    /// <summary>
    /// Picks a random entry based on weights
    /// </summary>
    private SpawnEntry? PickWeightedEntry(List<(SpawnEntry entry, float weight)> entries)
    {
        if (entries.Count == 0)
            return null;

        var totalWeight = entries.Sum(e => e.weight);
        if (totalWeight <= 0)
            return entries[0].entry;

        var roll = _random.NextFloat() * totalWeight;
        var cumulative = 0f;

        foreach (var (entry, weight) in entries)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return entry;
        }

        return entries[^1].entry;
    }

    /// <summary>
    /// Updates the UI with current component state
    /// </summary>
    private void UpdateUserInterface(EntityUid uid, UniversalSpawnerComponent component)
    {
        if (!_uiSystem.HasUi(uid, UniversalSpawnerUiKey.Key))
            return;

        // Get all available holidays
        var holidays = new List<string> { "" }; // Empty string for "No Holiday"
        foreach (var holiday in _prototypeManager.EnumeratePrototypes<HolidayPrototype>())
        {
            holidays.Add(holiday.ID);
        }

        var gamerules = new List<string>();
        foreach (var proto in _prototypeManager.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract)
                continue;

            if (proto.Components.ContainsKey(_componentFactory.GetComponentName(typeof(GameRuleComponent))))
            {
                gamerules.Add(proto.ID);
            }
        }

        var state = new UniversalSpawnerBoundUserInterfaceState(
            new List<SpawnEntry>(component.Entries),
            component.MaxSpawns,
            component.Offset,
            component.DeleteAfterSpawn,
            component.SpawnChance,
            component.MinSpawns,
            component.HasSpawned,
            holidays,
            component.MinRolls,
            component.MaxRolls,
            component.TriggerType,
            component.TriggerTimeSeconds,
            component.TriggerGameRule,
            component.ProximityRange,
            gamerules
        );

        _uiSystem.SetUiState(uid, UniversalSpawnerUiKey.Key, state);
    }
}
