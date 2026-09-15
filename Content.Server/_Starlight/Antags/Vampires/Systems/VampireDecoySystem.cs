using Content.Server._Starlight.Antags.Vampires.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Flash;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Starlight.Antags.Vampires.Systems;

public sealed partial class VampireDecoySystem : EntitySystem
{
    [Dependency] private SharedFlashSystem _flash = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VampireDecoyComponent, DamageChangedEvent>(OnDecoyDamaged);
    }

    private void OnDecoyDamaged(EntityUid uid, VampireDecoyComponent component, DamageChangedEvent args)
    {
        if (component.Detonated || args.DamageDelta == null || !args.DamageDelta.AnyPositive())
            return;

        component.Detonated = true;
        TriggerDecoyFlash((uid, component));
    }

    private void TriggerDecoyFlash(Entity<VampireDecoyComponent> ent)
    {
        var (uid, comp) = ent;
        var coords = _transform.GetMapCoordinates(uid);
        var entityCoords = Transform(uid).Coordinates;

        _flash.FlashArea(uid, null, comp.FlashRange, comp.FlashDuration, slowTo: comp.SlowTo, displayPopup: comp.DisplayPopup, probability: comp.Probability);
        _audio.PlayPvs(comp.FlashSound, entityCoords, AudioParams.Default.WithVolume(1f).WithMaxDistance(comp.FlashRange));

        EntityManager.SpawnEntity(comp.FlashEffectId, coords);
        QueueDel(uid);
    }
}
