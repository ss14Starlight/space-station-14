using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.NodeGroups;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using Robust.Shared.Timing;
using Content.Server.Radiation.Systems;
using Content.Server.Radiation.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.Reactions;

/// <summary>
///     Funky Atmos - /tg/ gases
///     Consumes a tiny amount of tritium to convert CO2 and oxygen to pluoxium.
/// </summary>
[UsedImplicitly]
public sealed partial class PluoxiumRadiationProductionReaction : IGasReactionEffect
{
    private IEntityManager entityManager => IoCManager.Resolve<IEntityManager>();
    private IGameTiming gameTiming => IoCManager.Resolve<IGameTiming>();
    private IEntitySystemManager systemManager => IoCManager.Resolve<IEntitySystemManager>();
    private RadiationSystem radiationSystem => systemManager.GetEntitySystem<RadiationSystem>();
    private SharedMapSystem mapSystem => systemManager.GetEntitySystem<SharedMapSystem>();
    private const float RadiationThreshold = 0.01f;
    private static readonly TimeSpan TimerDuration = TimeSpan.FromSeconds(5);
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), EntityUid> _tileReceivers = new();

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (entityManager == null || gameTiming == null)
            return ReactionResult.NoReaction;

        float initO2 = mixture.GetMoles(Gas.Oxygen);
        float initCO2 = mixture.GetMoles(Gas.CarbonDioxide);

        float radiationLevel = 0f;

        if (holder is null)
        {
            return ReactionResult.NoReaction;
        }
        else if (holder is Component component)
        {
            var owner = component.Owner;
            radiationLevel = GetRadiationLevel(owner);
        }
        else if (holder is TileAtmosphere tile)
        {
            // Starlight start
            var tileRef = atmosphereSystem.GetTileRef(tile);
            var coords = mapSystem.ToCenterCoordinates(tileRef);
            var gridUid = tileRef.GridUid;
            var gridComp = entityManager.GetComponent<MapGridComponent>(gridUid);
            var tileIndices = mapSystem.TileIndicesFor((gridUid, gridComp), coords);
            var tileLookup = (tileRef.GridUid, tileIndices);
            // create a temporary entity to actaully be able to measure the radiation on the tile if one doesnt already exist
            if (!_tileReceivers.TryGetValue(tileLookup, out var receiver) || entityManager.Deleted(receiver))
            {
                receiver = entityManager.SpawnEntity(null, coords);

                entityManager.EnsureComponent<RadiationReceiverComponent>(receiver);

                var timer = entityManager.EnsureComponent<RadiationReceiverTimerComponent>(receiver);
                timer.TimerExpiresAt = gameTiming.CurTime + TimerDuration;
                timer.DeleteEntityOnExpire = true;

                _tileReceivers[tileLookup] = receiver;

                return ReactionResult.Reacting;
            }
            var receiverTimer = entityManager.EnsureComponent<RadiationReceiverTimerComponent>(receiver);

            receiverTimer.TimerExpiresAt = gameTiming.CurTime + TimerDuration;
            radiationLevel = entityManager.GetComponent<RadiationReceiverComponent>(receiver).CurrentRadiation;
            // Starlight end
        }
        else if (holder is PipeNet pipeNet) // Very resource heavy. Could be disabled or commented out with flavor being pipes have some kind of shielding.
        {
            float totalRads = 0f;
            int totalNodes = pipeNet.Nodes.Count;

            if (totalNodes == 0)
                return ReactionResult.NoReaction;

            var xformQuery = entityManager.GetEntityQuery<TransformComponent>();

            foreach (var node in pipeNet.Nodes)
            {
                if (!xformQuery.TryGetComponent(node.Owner, out var xform))
                    continue;

                var coords = xform.Coordinates;
                var nodeRads = radiationSystem.GetRadiationAtCoordinates(coords);

                // Always request sampling so the cache stays fresh for the next atmos tick
                if (nodeRads < 0.001f)
                    radiationSystem.RequestTileRadiationSampling(coords);

                // Add the rads to the total
                totalRads += nodeRads;
            }

            // Average across the entire network
            radiationLevel = totalRads / totalNodes;
        }

        if (radiationLevel < RadiationThreshold)
            return ReactionResult.Reacting;

        float producedAmount = Math.Min(radiationLevel, Math.Min(initCO2, initO2 * 2f));

        float co2Removed = producedAmount;
        float oxyRemoved = producedAmount * 0.5f;

        if (co2Removed > initCO2 ||
            oxyRemoved > initO2)
            return ReactionResult.NoReaction;

        if (producedAmount <= 0)
            return ReactionResult.Reacting;

        mixture.AdjustMoles(Gas.CarbonDioxide, -co2Removed);
        mixture.AdjustMoles(Gas.Oxygen, -oxyRemoved);
        mixture.AdjustMoles(Gas.Pluoxium, producedAmount);

        float energyReleased = producedAmount * Atmospherics.PluoxiumProductionEnergy;
        float heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (heatCap > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * heatCap + energyReleased) / heatCap, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }

    private float GetRadiationLevel(EntityUid entity)
    {
        bool hadReceiver = entityManager.HasComponent<RadiationReceiverComponent>(entity);
        var receiverComp = entityManager.EnsureComponent<RadiationReceiverComponent>(entity);

        if (!hadReceiver)
        {
            var timerComp = entityManager.EnsureComponent<RadiationReceiverTimerComponent>(entity);
            timerComp.TimerExpiresAt = gameTiming.CurTime + TimerDuration;
        }
        else if (entityManager.TryGetComponent<RadiationReceiverTimerComponent>(entity, out var timerComp))
        {
            timerComp.TimerExpiresAt = gameTiming.CurTime + TimerDuration;
        }

        return receiverComp.CurrentRadiation;
    }
}

[RegisterComponent]
public sealed partial class RadiationReceiverTimerComponent : Component
{
    public TimeSpan TimerExpiresAt { get; set; } = TimeSpan.Zero;

    public bool DeleteEntityOnExpire { get; set; } = false; // Starlight
}

public sealed partial class RadiationTimerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEntityManager entityManager = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = entityManager.EntityQueryEnumerator<RadiationReceiverTimerComponent>();
        while (query.MoveNext(out var uid, out var timer))
        {
            // Starlight start
            if (_timing.CurTime < timer.TimerExpiresAt)
                continue;
            // cleanup of inactive temporary radiation recievers
            if (timer.DeleteEntityOnExpire)
            {
                entityManager.QueueDeleteEntity(uid);
            }
            else
            // Starlight end
            {
                entityManager.RemoveComponent<RadiationReceiverComponent>(uid);
                entityManager.RemoveComponent<RadiationReceiverTimerComponent>(uid);
            }
        }
    }
}
