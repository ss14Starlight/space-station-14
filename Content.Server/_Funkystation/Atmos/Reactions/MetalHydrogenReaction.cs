using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Funkystation.Atmos.Reactions;

/// <summary>
///     Funky Atmos - /tg/ gases
///     The formation of metal hydrogen from the mixture of BZ and Hydrogen at temperatures below 273.15K.
/// </summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class MetalHydrogenReaction : IGasReactionEffect
{
    private const float RequiredH2 = 300f;
    private const float RequiredBZ = 50f;
    private const float MinPressure = 10000f;
    private const float PressureThreshold = 20000f;
    private const float TemperatureThreshold = 50f;
    private const float BaseRate = 0.10f;

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (holder is not TileAtmosphere tile ||
            mixture.Pressure < MinPressure)
            return ReactionResult.NoReaction;

        var entityManager = IoCManager.Resolve<IEntityManager>();
        var random = IoCManager.Resolve<IRobustRandom>();
        var mapSystem = entityManager.System<SharedMapSystem>();

        float pressureEfficiency = Math.Min(mixture.Pressure / PressureThreshold, 1f);
        float temperatureEfficiency = Math.Min(TemperatureThreshold / mixture.Temperature, 1f);
        float rate = pressureEfficiency * temperatureEfficiency * BaseRate;

        if (random.NextFloat() > rate)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.Hydrogen, -RequiredH2);
        mixture.AdjustMoles(Gas.BZ, -RequiredBZ);

        var tileRef = atmosphereSystem.GetTileRef(tile);
        if (!entityManager.TryGetComponent<MapGridComponent>(tileRef.GridUid, out var grid))
            return ReactionResult.NoReaction;

        var coords = mapSystem.GridTileToLocal(tileRef.GridUid, grid, tileRef.GridIndices);
        entityManager.SpawnEntity("MetalHydrogen1", coords);

        return ReactionResult.Reacting;
    }
}
