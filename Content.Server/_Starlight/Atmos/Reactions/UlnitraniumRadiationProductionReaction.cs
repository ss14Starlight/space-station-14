using Content.Server.Atmos;
using Content.Server.Atmos.Reactions;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.NodeGroups;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using Robust.Shared.Timing;
using Content.Server.Radiation.Systems;
using Content.Server.Radiation.Components;


namespace Content.Server._Starlight.Atmos.Reactions;

/// <summary>
///     Based on Funky Atmos
///     Uses radiation to convert N2O and nitrogen to ulnitranium.
/// </summary>
[UsedImplicitly]
public sealed partial class UlnitraniumRadiationProductionReaction : IGasReactionEffect
{
    private IEntityManager entityManager => IoCManager.Resolve<IEntityManager>();
    private IGameTiming gameTiming => IoCManager.Resolve<IGameTiming>();
    private IEntitySystemManager systemManager => IoCManager.Resolve<IEntitySystemManager>();
    private RadiationSystem radiationSystem => systemManager.GetEntitySystem<RadiationSystem>();
    private SharedMapSystem mapSystem => systemManager.GetEntitySystem<SharedMapSystem>();
    private const float RadiationThreshold = 0.01f;
    private static readonly TimeSpan TimerDuration = TimeSpan.FromSeconds(5);

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (entityManager == null || gameTiming == null)
            return ReactionResult.NoReaction;

        float initN2 = mixture.GetMoles(Gas.Nitrogen);
        float initN2O = mixture.GetMoles(Gas.NitrousOxide);

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
            var tileRef = atmosphereSystem.GetTileRef(tile);
            var gridUid = tileRef.GridUid;

            // We use the center of the tile to ensure the Raycast actually hits the intended area
            var coords = mapSystem.ToCenterCoordinates(tileRef);
            radiationLevel = radiationSystem.GetRadiationAtCoordinates(coords);

            // If we have no data, or data is too low, we MUST request a sample for the future
            if (radiationLevel < RadiationThreshold)
            {
                radiationSystem.RequestTileRadiationSampling(coords);
                return ReactionResult.Reacting;
            }
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

        float producedAmount = Math.Min(radiationLevel, Math.Min(initN2O, initN2 * 2f));

        float n2oRemoved = producedAmount;
        float nitRemoved = producedAmount * 0.5f;

        if (n2oRemoved > initN2O ||
            nitRemoved > initN2)
            return ReactionResult.NoReaction;

        if (producedAmount <= 0)
            return ReactionResult.Reacting;

        mixture.AdjustMoles(Gas.NitrousOxide, -n2oRemoved);
        mixture.AdjustMoles(Gas.Nitrogen, -nitRemoved);
        mixture.AdjustMoles(Gas.Ulnitranium, producedAmount);

        float energyReleased = producedAmount * Atmospherics.UlnitraniumProductionEnergy;
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
