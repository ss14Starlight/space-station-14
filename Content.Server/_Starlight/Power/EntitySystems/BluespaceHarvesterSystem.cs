using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Starlight.Power.BluespaceHarvester;
using Content.Shared.Random.Helpers;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Power.EntitySystems;

/// <summary>
/// Manages Bluespace Harvester machines that convert electrical power into OWN research points.
/// Points can be spent to spawn items from configurable loot pools.
/// </summary>
public sealed class BluespaceHarvesterSystem : EntitySystem
{
    private const float PowerEpsilon = 0.01f;

    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PowerNetSystem _powerNet = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BluespaceHarvesterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BluespaceHarvesterComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<BluespaceHarvesterComponent, BluespaceHarvesterSetLevelMessage>(OnSetLevel);
        SubscribeLocalEvent<BluespaceHarvesterComponent, BluespaceHarvesterPurchaseMessage>(OnPurchase);
        SubscribeLocalEvent<BluespaceHarvesterPortalComponent, ComponentShutdown>(OnPortalShutdown);
    }

    private void OnMapInit(EntityUid uid, BluespaceHarvesterComponent component, MapInitEvent args)
    {
        if (!TryComp<PowerConsumerComponent>(uid, out var powerConsumer))
            return;

        component.DesiredLevel = ClampDesiredLevel(component, component.DesiredLevel);
        UpdateDrawRate(component, powerConsumer);
        UpdateUi(uid, component, powerConsumer);
    }

    private void OnBeforeUiOpen(EntityUid uid, BluespaceHarvesterComponent component, BeforeActivatableUIOpenEvent args)
    {
        if (!TryComp<PowerConsumerComponent>(uid, out var powerConsumer))
            return;

        UpdateUi(uid, component, powerConsumer, true);
    }

    private void OnSetLevel(EntityUid uid, BluespaceHarvesterComponent component, BluespaceHarvesterSetLevelMessage args)
    {
        if (!TryComp<PowerConsumerComponent>(uid, out var powerConsumer))
            return;

        var desired = ClampDesiredLevel(component, args.Level);
        if (desired == component.DesiredLevel)
            return;

        component.DesiredLevel = desired;
        UpdateDrawRate(component, powerConsumer);
        UpdateUi(uid, component, powerConsumer, true);
    }

    private void OnPurchase(EntityUid uid, BluespaceHarvesterComponent component, BluespaceHarvesterPurchaseMessage args)
    {
        if (!TryComp<PowerConsumerComponent>(uid, out var powerConsumer))
            return;

        if (component.IsBlocked)
            return;

        if (!_prototype.TryIndex<BluespaceHarvesterPoolPrototype>(args.PoolId, out var pool))
            return;

        if (!pool.Enabled) // safety check
            return;

        if (component.Points < pool.Cost)
            return;

        var lootTable = _prototype.Index(pool.LootTable);
        var spawned = lootTable.Pick(_random);

        component.Points -= pool.Cost;

        // spawn purchased item towards front of harvester
        // TODO: Consider making spawn offset configurable or finding empty tile
        var xform = Transform(uid);
        var spawnCoords = xform.Coordinates.Offset(new Vector2(0f, -0.75f));
        Spawn(spawned, spawnCoords);

        UpdateUi(uid, component, powerConsumer, true);
    }

    private void OnPortalShutdown(EntityUid uid, BluespaceHarvesterPortalComponent portal, ComponentShutdown args)
    {
        if (portal.SourceHarvester == null || !TryComp<BluespaceHarvesterComponent>(portal.SourceHarvester, out var harvester))
            return;

        // Unblock the harvester when the portal is deleted
        if (harvester.ActivePortal == uid)
        {
            harvester.IsBlocked = false;
            harvester.ActivePortal = null;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<BluespaceHarvesterComponent, PowerConsumerComponent>();
        while (query.MoveNext(out var uid, out var component, out var powerConsumer))
        {
            if (curTime - component.LastUpdate < component.UpdateDelay)
                continue;

            var deltaTime = (float)(curTime - component.LastUpdate).TotalSeconds;
            component.LastUpdate = curTime;

            if (component.ActivePortal != null && !Exists(component.ActivePortal.Value))
            {
                component.ActivePortal = null;
                component.IsBlocked = false;
            }
            if (component.IsBlocked)
            {
                UpdateUi(uid, component, powerConsumer);
                continue;
            }

            UpdateDrawRate(component, powerConsumer);

            var currentLevel = CalculateCurrentLevel(component, powerConsumer.ReceivedPower);
            if (currentLevel != component.CurrentLevel)
                component.CurrentLevel = currentLevel;

            // Only generate points when machine is actively working
            if (component.CurrentLevel > 0 && powerConsumer.ReceivedPower > 0)
            {
                // Points generation formula => base rate per level + bonus Based on actual power consumption
                var pointsPerSecond = (component.PointsPerLevel * component.CurrentLevel)
                    + (component.PointsPerMegawatt * (powerConsumer.ReceivedPower / 1_000_000f));

                // Accumulator pattern, collect fractional points over time, award whole points when >= 1
                // Prevents losing fractional points each tick
                component.PointAccumulator += pointsPerSecond * deltaTime;
                if (component.PointAccumulator >= 1f)
                {
                    var added = (int) component.PointAccumulator;
                    component.PointAccumulator -= added;
                    component.Points += added;
                    component.TotalPoints += added;
                }
            }

            // Check for portal spawn in dangerous mode
            if (component.CurrentLevel >= component.DangerousLevelThreshold)
            {
                TrySpawnPortal(uid, component, deltaTime);
            }

            UpdateUi(uid, component, powerConsumer);
        }
    }

    private void TrySpawnPortal(EntityUid uid, BluespaceHarvesterComponent component, float frameTime)
    {
        // Dont spawn another portal while one is active
        if (component.IsBlocked || component.ActivePortal != null)
            return;

        var levelsAboveThreshold = component.CurrentLevel - component.DangerousLevelThreshold;

        // Calculate chances
        var chancePerSecond = component.BasePortalChancePerSecond +
                              (levelsAboveThreshold * component.PortalChancePerLevelAboveThreshold);

        component.PortalAccumulator += frameTime;

        if (component.PortalAccumulator < 1f)
            return;

        component.PortalAccumulator -= 1f;

        if (!_random.Prob((float)chancePerSecond))
            return;

        // Spawn the portal near the harvester
        var xform = Transform(uid);
        var angle = _random.NextAngle();
        var distance = _random.NextFloat(component.PortalMinDistance, component.PortalMaxDistance);
        var offset = angle.ToVec() * distance;
        var portalCoords = xform.Coordinates.Offset(offset);

        var portalUid = Spawn(component.PortalPrototype, portalCoords);

        if (TryComp<BluespaceHarvesterPortalComponent>(portalUid, out var portalComp))
            portalComp.SourceHarvester = uid;

        component.ActivePortal = portalUid;
        component.IsBlocked = true;
        component.DesiredLevel = 0; // Reset level to 0 when portal spawns

        var mobCount = _random.Next(component.MinMobsPerPortal, component.MaxMobsPerPortal + 1);
        for (var i = 0; i < mobCount; i++)
        {
            var mobProto = _random.Pick(component.PortalMobPrototypes);
            var mobAngle = _random.NextAngle();
            var mobOffset = mobAngle.ToVec() * _random.NextFloat(0.5f, 1.5f);
            var mobCoords = portalCoords.Offset(mobOffset);
            Spawn(mobProto, mobCoords);
        }
        var msg = Loc.GetString("bluespace-harvester-portal-warning");
        _chat.DispatchGlobalAnnouncement(msg, playSound: true, colorOverride: Color.Red);
    }

    private static int ClampDesiredLevel(BluespaceHarvesterComponent component, int level)
    {
        var max = component.LevelPowerDraw.Count;
        if (max <= 0)
            return 0;

        return Math.Clamp(level, 0, max);
    }

    /// <summary>
    /// Determines the actual operating level based on available power.
    /// The machine operates at the highest level it can sustain with current power.
    /// </summary>
    private static int CalculateCurrentLevel(BluespaceHarvesterComponent component, float receivedPower)
    {
        if (component.DesiredLevel <= 0 || component.LevelPowerDraw.Count == 0)
            return 0;

        // Cant exceed desired level even if we have power for more
        var maxLevel = Math.Min(component.DesiredLevel, component.LevelPowerDraw.Count);
        var current = 0;

        // Find highest level we can sustain with current power
        for (var i = 0; i < maxLevel; i++)
        {
            if (receivedPower + PowerEpsilon >= component.LevelPowerDraw[i])
                current = i + 1;
        }

        return current;
    }

    private static float GetPowerForNextLevel(BluespaceHarvesterComponent component, int currentLevel)
    {
        if (component.LevelPowerDraw.Count == 0)
            return 0f;

        var index = Math.Clamp(currentLevel, 0, component.LevelPowerDraw.Count - 1);
        return component.LevelPowerDraw[index];
    }

    private static void UpdateDrawRate(BluespaceHarvesterComponent component, PowerConsumerComponent powerConsumer)
    {
        if (component.DesiredLevel <= 0 || component.IsBlocked)
        {
            powerConsumer.DrawRate = 0f;
            return;
        }

        var index = Math.Clamp(component.DesiredLevel - 1, 0, component.LevelPowerDraw.Count - 1);
        powerConsumer.DrawRate = component.LevelPowerDraw[index];
    }

    private void UpdateUi(EntityUid uid, BluespaceHarvesterComponent component, PowerConsumerComponent powerConsumer, bool force = false)
    {
        var currentPower = powerConsumer.ReceivedPower;
        var powerForNext = GetPowerForNextLevel(component, component.CurrentLevel);

        // Get theoretical network supply from power network
        var networkSupply = 0f;
        if (powerConsumer.Net?.NetworkNode is { } networkNode)
        {
            var stats = _powerNet.GetNetworkStatistics(networkNode);
            networkSupply = stats.SupplyTheoretical;
        }

        var pools = _prototype.EnumeratePrototypes<BluespaceHarvesterPoolPrototype>()
            .Where(pool => pool.Enabled)
            .OrderBy(pool => pool.Order)
            .ThenBy(pool => pool.ID)
            .Select(pool => new BluespaceHarvesterPoolEntry(pool.ID, pool.Name, pool.Cost, pool.Enabled))
            .ToArray();

        var changed = force
                      || component.LastUiCurrentLevel != component.CurrentLevel
                      || component.LastUiDesiredLevel != component.DesiredLevel
                      || component.LastUiPoints != component.Points
                      || component.LastUiTotalPoints != component.TotalPoints
                      || component.LastUiIsBlocked != component.IsBlocked
                      || !MathHelper.CloseTo(component.LastUiCurrentPower, currentPower)
                      || !MathHelper.CloseTo(component.LastUiNextPower, powerForNext)
                      || !MathHelper.CloseTo(component.LastUiNetworkSupply, networkSupply);

        if (!changed)
            return;

        component.LastUiCurrentLevel = component.CurrentLevel;
        component.LastUiDesiredLevel = component.DesiredLevel;
        component.LastUiPoints = component.Points;
        component.LastUiTotalPoints = component.TotalPoints;
        component.LastUiCurrentPower = currentPower;
        component.LastUiNextPower = powerForNext;
        component.LastUiNetworkSupply = networkSupply;
        component.LastUiIsBlocked = component.IsBlocked;

        var state = new BluespaceHarvesterUiState(
            component.CurrentLevel,
            component.DesiredLevel,
            component.LevelPowerDraw.Count,
            currentPower,
            powerForNext,
            networkSupply,
            component.Points,
            component.TotalPoints,
            pools,
            component.IsBlocked);

        _ui.SetUiState(uid, BluespaceHarvesterUiKey.Key, state);
    }
}

