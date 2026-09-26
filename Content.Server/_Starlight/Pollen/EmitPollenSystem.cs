using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared._Starlight.Pollen.Components;
using Content.Shared._Starlight.Scent.Components;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using Content.Server._Starlight.Scent.Systems;
using Content.Shared.Botany.Items.Components;

namespace Content.Server._Starlight.Pollen.Systems;

public sealed partial class EmitPollenSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ScentSystem _scent = default!;

    private const string PollenMarkerPrototype = "ScentMarker";

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
            if (!TryComp<ProduceComponent>(uid, out var produce) || produce.PlantProtoId is not { } plantId)
                return;

            pollenId = plantId.ToString();
        }

        var lifetime = TimeSpan.FromSeconds(pollen.PollenLifetime);

        if (_scent.TryMergePollen(pollenId, transform, lifetime))
            return;

        var marker = SpawnAtPosition(PollenMarkerPrototype, transform.Coordinates);

        if (!TryComp<ScentMarkerComponent>(marker, out var markerComp))
        {
            Del(marker);
            return;
        }

        markerComp.ScentId = pollenId;
        markerComp.IsPollen = true;
        markerComp.Strength = 1f;
        markerComp.ExpiresAt = _timing.CurTime + lifetime;
        markerComp.TotalDuration = lifetime;

        Dirty(marker, markerComp);

        if (TryComp<TimedDespawnComponent>(marker, out var despawn))
            despawn.Lifetime = pollen.PollenLifetime;
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