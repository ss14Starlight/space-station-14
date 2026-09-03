using System.Collections.Immutable;
using System.Linq;
using Content.Shared.Audio;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Starlight.Sound;

public sealed partial class DamageThresholdSoundsSystem : EntitySystem
{
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageThresholdSoundsComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<DamageThresholdSoundsComponent> ent, ref DamageChangedEvent args)
    {
        var (uid, comp) = ent;
        if (!TryComp<DamageableComponent>(uid, out var damageComp))
            return;

        var damage = _damage.GetDamage((uid, damageComp)).GetTotal();
        FixedPoint2 selectedThreshold = 0;
        ThresholdSoundData? selectedSound = null;

        foreach (var (threshold, sound) in comp.Thresholds.ToImmutableSortedDictionary())
        {
            if (threshold > damage)
                break;

            selectedThreshold = threshold;
            selectedSound = sound;
        }

        if (comp.CurrentThreshold == selectedThreshold) return;
        comp.CurrentThreshold = selectedThreshold;
        if (selectedSound?.Sound is null) _ambient.SetAmbience(uid, false);
        else
        {
            if (selectedSound.Ambient)
            {
                EnsureComp<AmbientSoundComponent>(uid);
                _ambient.SetSound(uid, selectedSound.Sound);
                return;
            }

            _ambient.SetAmbience(uid, false);
            _audio.PlayPredicted(selectedSound.Sound, uid, uid, selectedSound.Sound.Params);
        }
    }
}
