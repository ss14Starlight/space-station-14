using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared._Starlight.Pollen.Components;
using Robust.Shared.Random;

using Robust.Shared.Timing;
using Content.Server._Starlight.Scent.Systems;
using Content.Shared.Botany.Items.Components;

using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Pollen;

public sealed partial class EmitPollenSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ScentSystem _scent = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    /// <inheritdoc />
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<EmitPollenComponent>();

        while (query.MoveNext(out var uid, out var pollen))
        {
            if (pollen.NextEmitTime == TimeSpan.Zero)
                pollen.NextEmitTime = now + RollEmitDelay(pollen);

            if (now < pollen.NextEmitTime)
                continue;

            pollen.NextEmitTime = now + RollEmitDelay(pollen);

            EmitPollen(uid, pollen);
        }
    }

    private void EmitPollen(EntityUid uid, EmitPollenComponent pollen)
    {
        if (!TryComp(uid, out TransformComponent? transform))
            return;

        var pollenId = GetPollenId(uid, pollen);
        if (pollenId == null)
            return;

        var lifetime = TimeSpan.FromSeconds(pollen.PollenLifetime);
        _scent.EmitPollenMarker(ref pollen.LastMarkerEntity, pollenId, transform, lifetime, pollen.MergeRadius, pollen.MergeStrengthStep);
    }

    private TimeSpan RollEmitDelay(EmitPollenComponent pollen)
    {
        var variance = pollen.EmitInterval * pollen.EmitIntervalVariance;

        var seconds = Math.Max(
            pollen.MinEmitInterval,
            pollen.EmitInterval + _random.NextFloat(-variance, variance));

        return TimeSpan.FromSeconds(seconds);
    }

    // Since plants are their own things now we use the relation of the product to get the id.
    private string? GetPollenId(EntityUid uid, EmitPollenComponent pollen)
    {
        // Optional override for unusual cases.
        if (pollen.PollenId is { } pollenId)
            return pollenId;

        // Harvested products: resolve through Produce.PlantProtoId.
        if (TryComp<ProduceComponent>(uid, out var produce) &&
            produce.PlantProtoId is { } plantId)
            //return NormalizePollenId(plantId.ToString());
            return plantId.ToString();

        // Growing plants: resolve through their product prototypes.
        if (!TryComp<PlantDataComponent>(uid, out var plantData))
            return null;

        string? resolvedId = null;

        foreach (var productId in plantData.ProductPrototypes)
        {
            var prototype = _prototypeManager.Index(productId);

            if (!prototype.TryGetComponent<ProduceComponent>(out var product) ||
                product.PlantProtoId is not { } productPlantId)
                continue;

            //var productPollenId = NormalizePollenId(productPlantId.ToString());
            var productPollenId = productPlantId.ToString();

            if (resolvedId != null && resolvedId != productPollenId)
                return null;

            resolvedId = productPollenId;
        }

        return resolvedId;
    }

    /*
    private static string NormalizePollenId(string plantId)
    {
        const string suffix = "Plants";
        return plantId.EndsWith(suffix, StringComparison.Ordinal) ? plantId[..^suffix.Length] : plantId;
    }
    */
}