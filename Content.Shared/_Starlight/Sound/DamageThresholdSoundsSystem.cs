using System.Collections.Immutable;
using Content.Shared.Audio;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Starlight.Sound;

public sealed partial class DamageThresholdSoundsSystem : EntitySystem
{
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private INetManager _net = default!;


    [SubscribeLocalEvent]
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
        if (selectedSound?.Sound is null)
        {
            _ambient.SetAmbience(uid, false);
            ent.Comp.AudioStream?.Comp?.StopPlaying(); //TODO Remove once RT bug fixed
            ent.Comp.AudioStream = _audio.Stop(comp.AudioStream);
        }
        else
        {
            //TODO Ambient sounds currently cannot stop due to an RT bug, uncomment this if they ever get around to fixing that
            //if (selectedSound.Ambient)
            //{
            //    if (ent.Comp.IsEmped && selectedSound.EmpEffected)
            //        return;
            //    EnsureComp<AmbientSoundComponent>(uid);
            //    _ambient.SetSound(uid, selectedSound.Sound);
            //    return;
            //}

            _ambient.SetAmbience(uid, false);
            ent.Comp.AudioStream?.Comp?.StopPlaying(); //TODO Remove once RT bug fixed
            ent.Comp.AudioStream = _audio.Stop(comp.AudioStream);
            if(ent.Comp.IsEmped && selectedSound.EmpEffected)
                return;
            if(_net.IsServer) //TODO Remove once RT bug fixed
                ent.Comp.AudioStream ??= _audio.PlayPvs(selectedSound.Sound, uid, selectedSound.Sound.Params);
        }
    }

    [SubscribeLocalEvent]
    private void OnEmpPulse(EntityUid uid, DamageThresholdSoundsComponent comp, ref EmpPulseEvent args)
    {
        args.Affected = true;
        args.Disabled = true;

        comp.IsEmped = true;
        Dirty(uid, comp);

        var selectedThreshold = comp.Thresholds[comp.CurrentThreshold];
        if(selectedThreshold == null)
            return;

        if(!selectedThreshold.EmpEffected)
            return;

        _ambient.SetAmbience(uid, false);
        comp.AudioStream?.Comp?.StopPlaying(); //TODO Remove once RT bug fixed
        comp.AudioStream = _audio.Stop(comp.AudioStream);
    }

    [SubscribeLocalEvent]
    private void OnEmpRemoved(EntityUid uid, DamageThresholdSoundsComponent comp, ref EmpDisabledRemovedEvent args)
    {
        comp.IsEmped = false;
        Dirty(uid, comp);

        var selectedThreshold = comp.Thresholds[comp.CurrentThreshold];
        if(selectedThreshold?.Sound == null)
            return;

        if(!selectedThreshold.EmpEffected)
            return;

        //if (selectedThreshold.Ambient)//TODO Ambient sounds currently cannot stop due to an RT bug, uncomment this if they ever get around to fixing that
        //{
        //    //EnsureComp<AmbientSoundComponent>(uid);
        //    //_ambient.SetSound(uid, selectedThreshold.Sound);
        //}
        //else
        //{
        if(_net.IsServer) //TODO Remove once RT bug fixed
            comp.AudioStream ??= _audio.PlayPvs(selectedThreshold.Sound, uid, selectedThreshold.Sound.Params);
        //}
    }
}
