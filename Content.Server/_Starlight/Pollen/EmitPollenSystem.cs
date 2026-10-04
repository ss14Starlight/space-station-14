using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared._Starlight.Pollen.Components;
using Robust.Shared.Random;

using Robust.Shared.Timing;
using Content.Server._Starlight.Scent.Systems;
using Content.Shared.Botany.Items.Components;

namespace Content.Server._Starlight.Pollen.Systems;

public sealed partial class EmitPollenSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ScentSystem _scent = default!;

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

        var pollenId = pollen.PollenId;

        if (pollenId == null)
        {
            if (!TryComp<ProduceComponent>(uid, out var produce) ||
                produce.PlantProtoId is not { } plantId)
                return;

            pollenId = plantId.ToString();
        }

        var lifetime = TimeSpan.FromSeconds(pollen.PollenLifetime);
        _scent.EmitPollenMarker(ref pollen.LastMarkerEntity, pollenId, transform, lifetime);
    }

    private TimeSpan RollEmitDelay(EmitPollenComponent pollen)
    {
        var variance = pollen.EmitInterval * pollen.EmitIntervalVariance;

        var seconds = Math.Max(
            pollen.MinEmitInterval,
            pollen.EmitInterval + _random.NextFloat(-variance, variance));

        return TimeSpan.FromSeconds(seconds);
    }
}