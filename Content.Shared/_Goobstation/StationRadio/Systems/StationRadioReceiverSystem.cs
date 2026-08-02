using Content.Shared._Goobstation.StationRadio.Components;
using Content.Shared._Goobstation.StationRadio.Events;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Systems;
using Content.Shared.DeviceLinking;
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

        SubscribeLocalEvent<StationRadioServerComponent, PowerChangedEvent>(OnServerPowerChanged);

        SubscribeLocalEvent<StationRadioReceiverComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs); // Moffstation - Alt click to lower volume.
    }

    /// <summary>
    /// Resolves whether Radio Rig is connected to a Radio Server that has power,
    /// and whether or not it can broadcast.
    /// </summary>
    public bool TryGetLinkedPoweredServer(EntityUid uid, out EntityUid server)
    {
        server = default;

        if (!TryComp<DeviceLinkSourceComponent>(uid, out var source))
            return false;

        foreach (var linkedRig in source.LinkedPorts.Keys)
        {
            if (!HasComp<RadioRigComponent>(linkedRig) || !TryComp<DeviceLinkSinkComponent>(linkedRig, out var sink))
                continue;

            foreach (var linkedServer in sink.LinkedSources)
            {
                var hasComp = HasComp<StationRadioServerComponent>(linkedServer);
                var powered = _power.IsPowered(linkedServer);
                Log.Info($"[StationRadio] Candidate server {ToPrettyString(linkedServer)}: hasComp={hasComp}, powered={powered}");

                if (!hasComp || !powered)
                    continue;

                server = linkedServer;
                return true;
            }
        }

        return false;
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

    /// <summary>
    /// Stop broadcasting if the Radio Server loses power, despite the Vinyl Player and Rig still being powered.
    /// </summary>
    private void OnServerPowerChanged(EntityUid uid, StationRadioServerComponent comp, PowerChangedEvent args)
    {
        if (args.Powered)
            return;

        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out _))
        {
            RaiseLocalEvent(receiver, new StationRadioMediaStoppedEvent());
        }
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
