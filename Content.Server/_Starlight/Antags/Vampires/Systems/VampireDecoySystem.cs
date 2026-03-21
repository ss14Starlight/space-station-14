using Content.Server._Starlight.Antags.Vampires.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Flash;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Starlight.Antags.Vampires.Systems;

public sealed class VampireDecoySystem : EntitySystem
{
    private const string DecoyFlashEffectId = "GrenadeFlashEffect";
    private const float DecoyFlashRange = 3f;
    private static readonly TimeSpan _decoyFlashDuration = TimeSpan.FromSeconds(4);
    private static readonly SoundSpecifier _decoyFlashSound = new SoundPathSpecifier("/Audio/Weapons/flash.ogg");

    [Dependency] private readonly SharedFlashSystem _flash = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

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
        TriggerDecoyFlash(uid);
    }

    private void TriggerDecoyFlash(EntityUid uid)
    {
        var coords = _transform.GetMapCoordinates(uid);
        var entityCoords = Transform(uid).Coordinates;

        _flash.FlashArea(uid, null, DecoyFlashRange, _decoyFlashDuration, slowTo: 0.5f, displayPopup: true, probability: 1f);
        _audio.PlayPvs(_decoyFlashSound, entityCoords, AudioParams.Default.WithVolume(1f).WithMaxDistance(DecoyFlashRange));

        EntityManager.SpawnEntity(DecoyFlashEffectId, coords);
        QueueDel(uid);
    }
}
