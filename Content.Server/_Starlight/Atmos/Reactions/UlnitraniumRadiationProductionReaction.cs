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
    [Dependency] private IEntityManager _ent = null!;
    [Dependency] private IGameTiming _timing = null!;
    private readonly RadiationSystem _rads = null!;
    private readonly SharedMapSystem _map = null!;
    private const float RadiationThreshold = 0.01f;
    private static readonly TimeSpan _timerDuration = TimeSpan.FromSeconds(5);

    public UlnitraniumRadiationProductionReaction()
    {
        IoCManager.InjectDependencies(this);
        _rads = _ent.System<RadiationSystem>();
        _map = _ent.System<SharedMapSystem>();
    }

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

                    // We use the center of the tile to ensure the Raycast actually hits the intended area
                    var coords = _map.ToCenterCoordinates(tileRef);
                    radiationLevel = _rads.GetRadiationAtCoordinates(coords);

                    // If we have no data, or data is too low, we MUST request a sample for the future
                    if (radiationLevel < RadiationThreshold)
                    {
                        _rads.RequestTileRadiationSampling(coords);
                        return ReactionResult.Reacting;
                    }

                    break;
                }
            // Very resource heavy. Could be disabled or commented out with flavor being pipes have some kind of shielding.
            case PipeNet pipeNet:
                {
                    var totalRads = 0f;
                    var totalNodes = pipeNet.Nodes.Count;

                    if (totalNodes == 0)
                        return ReactionResult.NoReaction;

                    var xformQuery = _ent.GetEntityQuery<TransformComponent>();

                    foreach (var node in pipeNet.Nodes)
                    {
                        if (!xformQuery.TryGetComponent(node.Owner, out var xform))
                            continue;

                        var coords = xform.Coordinates;
                        var nodeRads = _rads.GetRadiationAtCoordinates(coords);

                        // Always request sampling so the cache stays fresh for the next atmos tick
                        if (nodeRads < 0.001f)
                            _rads.RequestTileRadiationSampling(coords);

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
        var hadReceiver = _ent.HasComponent<RadiationReceiverComponent>(entity);
        var receiverComp = _ent.EnsureComponent<RadiationReceiverComponent>(entity);

        if (!hadReceiver)
        {
            var timerComp = _ent.EnsureComponent<RadiationReceiverTimerComponent>(entity);
            timerComp.TimerExpiresAt = _timing.CurTime + _timerDuration;
        }
        else if (_ent.TryGetComponent<RadiationReceiverTimerComponent>(entity, out var timerComp))
            timerComp.TimerExpiresAt = _timing.CurTime + _timerDuration;

        return receiverComp.CurrentRadiation;
    }
}
