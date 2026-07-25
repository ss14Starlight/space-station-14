using Content.Shared._Goobstation.StationRadio.Components;
using Content.Shared._Goobstation.StationRadio.Events;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Systems;
using Content.Shared.Radio.Components; // Moffstation - Alt click to lower volume.
using Content.Shared.Verbs; // Moffstation - Alt click to lower volume.

namespace Content.Shared._Goobstation.StationRadio.Systems;

public sealed partial class StationRadioReceiverSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaPlayedEvent>(OnMediaPlayed);
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaStoppedEvent>(OnMediaStopped);
        SubscribeLocalEvent<StationRadioReceiverComponent, ActivateInWorldEvent>(OnRadioToggle);
        SubscribeLocalEvent<StationRadioReceiverComponent, PowerChangedEvent>(OnPowerChanged);

        SubscribeLocalEvent<StationRadioReceiverComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs); // Moffstation - Alt click to lower volume.
    }

    private void OnPowerChanged(EntityUid uid, StationRadioReceiverComponent comp, PowerChangedEvent args)
    {
        if(comp.SoundEntity == null)
            return;
        _audio.SetGain(comp.SoundEntity, GetGain(comp, args.Powered));
    }

    private void OnRadioToggle(EntityUid uid, StationRadioReceiverComponent comp, ActivateInWorldEvent args)
    {
        comp.Active = !comp.Active;
        Dirty(uid, comp);
        if (comp.SoundEntity != null)
            _audio.SetGain(comp.SoundEntity, GetGain(comp, _power.IsPowered(uid)));
    }

    private void OnMediaPlayed(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaPlayedEvent args)
    {
        var startParams = comp.DefaultParams.WithVolume(-100f);
        var sound = _audio.PlayPvs(args.MediaPlayed, uid, comp.DefaultParams);
        if (sound == null)
            return;

        comp.SoundEntity = sound.Value.Entity;
            _audio.SetGain(comp.SoundEntity, GetGain(comp, _power.IsPowered(uid)));
    }

    private void OnMediaStopped(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaStoppedEvent args)
    {
        if (comp.SoundEntity == null)
            return;

        comp.SoundEntity = _audio.Stop(comp.SoundEntity);
    }

    // Moffstation - Start - Alt click to lower volume.
    private static float GetGain(StationRadioReceiverComponent comp, bool powered)
    {
        if (!comp.Active || !powered)
            return 0f;

        return comp.LowVolume ? 0.1f : 1f;
    }

    private void OnGetAltVerbs(EntityUid uid, StationRadioReceiverComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = comp.LowVolume ? "Lower Volume" : "Increase Volume",
            Act = () =>
            {
                comp.LowVolume = !comp.LowVolume;
                Dirty(uid, comp);
                if (comp.SoundEntity != null)
                    _audio.SetGain(comp.SoundEntity, GetGain(comp, _power.IsPowered(uid)));
            }
        });
    }
    // Moffstation - End
}
