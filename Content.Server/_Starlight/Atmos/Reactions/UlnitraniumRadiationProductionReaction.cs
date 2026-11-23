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
using Robust.Shared.Map.Components;


namespace Content.Server._Starlight.Atmos.Reactions;

/// <summary>
///     Based on Funky Atmos
///     Uses radiation to convert N2O and nitrogen to ulnitranium.
/// </summary>
[UsedImplicitly]
public sealed partial class UlnitraniumRadiationProductionReaction : IGasReactionEffect
{
    private static IEntityManager s_ent => IoCManager.Resolve<IEntityManager>();
    private static IGameTiming s_timing => IoCManager.Resolve<IGameTiming>();
    private static RadiationSystem s_rads => s_ent.System<RadiationSystem>();
    private static SharedMapSystem s_map => s_ent.System<SharedMapSystem>();
    private const float RadiationThreshold = 0.01f;
    private static readonly TimeSpan _timerDuration = TimeSpan.FromSeconds(5);
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), EntityUid> _tileReceivers = new();

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem,
        float heatScale)
    {
        var initN2 = mixture.GetMoles(Gas.Nitrogen);
        var initN2O = mixture.GetMoles(Gas.NitrousOxide);

        var radiationLevel = 0f;

        switch (holder)
        {
            case null:
                return ReactionResult.NoReaction;
            case Component:
                {
                    if (atmosphereSystem.ReactingEntity is { } owner)
                        radiationLevel = GetRadiationLevel(owner);
                    break;
                }
            case TileAtmosphere tile:
                {
                    var tileRef = atmosphereSystem.GetTileRef(tile);
                    var coords = s_map.ToCenterCoordinates(tileRef);
                    var gridUid = tileRef.GridUid;
                    var gridComp = s_ent.GetComponent<MapGridComponent>(gridUid);
                    var tileIndices = s_map.TileIndicesFor((gridUid, gridComp), coords);
                    var tileLookup = (tileRef.GridUid, tileIndices);
                    // create a temporary entity to actaully be able to measure the radiation on the tile
                    if (!_tileReceivers.TryGetValue(tileLookup, out var receiver) || s_ent.Deleted(receiver))
                    {
                        receiver = s_ent.SpawnAtPosition(null, coords);

                        s_ent.EnsureComponent<RadiationReceiverComponent>(receiver);

                        var timer = s_ent.EnsureComponent<RadiationReceiverTimerComponent>(receiver);
                        timer.TimerExpiresAt = s_timing.CurTime + _timerDuration;
                        timer.DeleteEntityOnExpire = true;

                        _tileReceivers[tileLookup] = receiver;

                        return ReactionResult.Reacting;
                    }
                    var receiverTimer = s_ent.EnsureComponent<RadiationReceiverTimerComponent>(receiver);

                    receiverTimer.TimerExpiresAt = s_timing.CurTime + _timerDuration;
                    radiationLevel = s_ent.GetComponent<RadiationReceiverComponent>(receiver).CurrentRadiation;

                    break;
                }
            // Very resource heavy. Could be disabled or commented out with flavor being pipes have some kind of shielding.
            case PipeNet pipeNet:
                {
                    var totalRads = 0f;
                    var totalNodes = pipeNet.Nodes.Count;

                    if (totalNodes == 0)
                        return ReactionResult.NoReaction;

                    var xformQuery = s_ent.GetEntityQuery<TransformComponent>();

                    foreach (var node in pipeNet.Nodes)
                    {
                        if (!xformQuery.TryGetComponent(node.Owner, out var xform))
                            continue;

                        var coords = xform.Coordinates;
                        var nodeRads = s_rads.GetRadiationAtCoordinates(coords);

                        // Always request sampling so the cache stays fresh for the next atmos tick
                        if (nodeRads < 0.001f)
                            s_rads.RequestTileRadiationSampling(coords);

                        // Add the rads to the total
                        totalRads += nodeRads;
                    }

                    // Average across the entire network
                    radiationLevel = totalRads / totalNodes;
                    break;
                }
        }

        if (radiationLevel < RadiationThreshold)
            return ReactionResult.Reacting;

        var producedAmount = Math.Min(radiationLevel, Math.Min(initN2O, initN2 * 2f));

        var nitRemoved = producedAmount * 0.5f;

        if (producedAmount > initN2O ||
            nitRemoved > initN2)
            return ReactionResult.NoReaction;

        if (producedAmount <= 0)
            return ReactionResult.Reacting;

        mixture.AdjustMoles(Gas.NitrousOxide, -producedAmount);
        mixture.AdjustMoles(Gas.Nitrogen, -nitRemoved);
        mixture.AdjustMoles(Gas.Ulnitranium, producedAmount);

        var energyReleased = producedAmount * Atmospherics.UlnitraniumProductionEnergy;
        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (heatCap > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max(((mixture.Temperature * heatCap) + energyReleased) / heatCap,
                Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }

    private float GetRadiationLevel(EntityUid entity)
    {
        var hadReceiver = s_ent.HasComponent<RadiationReceiverComponent>(entity);
        var receiverComp = s_ent.EnsureComponent<RadiationReceiverComponent>(entity);

        if (!hadReceiver)
        {
            var timerComp = s_ent.EnsureComponent<RadiationReceiverTimerComponent>(entity);
            timerComp.TimerExpiresAt = s_timing.CurTime + _timerDuration;
        }
        else if (s_ent.TryGetComponent<RadiationReceiverTimerComponent>(entity, out var timerComp))
            timerComp.TimerExpiresAt = s_timing.CurTime + _timerDuration;

        return receiverComp.CurrentRadiation;
    }
}
