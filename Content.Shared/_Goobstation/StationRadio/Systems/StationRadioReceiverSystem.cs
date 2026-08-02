using Content.Shared._Goobstation.StationRadio.Components;
using Content.Shared._Goobstation.StationRadio.Events;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Systems;
using Content.Shared.DeviceLinking;
using Content.Shared.Radio.Components; // Moffstation - Alt click to lower volume.
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Timing; // Moffstation - Alt click to lower volume.

namespace Content.Shared._Goobstation.StationRadio.Systems;

public sealed partial class StationRadioReceiverSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaPlayedEvent>(OnMediaPlayed);
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaStoppedEvent>(OnMediaStopped);
        SubscribeLocalEvent<StationRadioReceiverComponent, ActivateInWorldEvent>(OnRadioToggle);
        SubscribeLocalEvent<StationRadioReceiverComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<StationRadioReceiverComponent, MapInitEvent>(OnReceiverMapInit);

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
        if (_net.IsClient)
            return;

        var sound = _audio.PlayPvs(args.MediaPlayed, uid, comp.DefaultParams);
        if (sound == null)
            return;

        comp.SoundEntity = sound.Value.Entity;

        if (args.PlayOffset > TimeSpan.Zero)
            _audio.SetPlaybackPosition(sound.Value.Entity, (float)args.PlayOffset.TotalSeconds);

        _audio.SetGain(comp.SoundEntity, GetGain(comp, _power.IsPowered(uid)));
    }

    private void OnMediaStopped(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaStoppedEvent args)
    {
        if (_net.IsClient)
            return;

        if (comp.SoundEntity == null)
            return;

        comp.SoundEntity = _audio.Stop(comp.SoundEntity);
    }

    /// <summary>
    /// When a station radio is initialised, check any active Radio server for if there is an
    /// active song playing. If there is, attempt to resume play.
    /// </summary>
    private void OnReceiverMapInit(EntityUid uid, StationRadioReceiverComponent comp, MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<StationRadioServerComponent>();
        while (query.MoveNext(out var server, out var serverComp))
        {
            if (serverComp.CurrentSong == null || serverComp.PlaybackStartTime == null || !_power.IsPowered(server))
                continue;

            var elapsed = _timing.CurTime - serverComp.PlaybackStartTime.Value;
            RaiseLocalEvent(uid, new StationRadioMediaPlayedEvent(serverComp.CurrentSong, elapsed));
            return;
        }
    }

    /// <summary>
    /// Stop broadcasting if the Radio Server loses power, despite the Vinyl Player and Rig still being powered.
    /// Resume play when the server power returns.
    /// </summary>
    private void OnServerPowerChanged(EntityUid uid, StationRadioServerComponent comp, PowerChangedEvent args)
    {
        if (_net.IsClient)
            return;

        if (!args.Powered)
        {
            var stopQuery = EntityQueryEnumerator<StationRadioReceiverComponent>();
            while (stopQuery.MoveNext(out var receiver, out _))
            {
                RaiseLocalEvent(receiver, new StationRadioMediaStoppedEvent());
            }
            return;
        }

        if (comp.CurrentSong == null || comp.PlaybackStartTime == null)
            return;

        var elapsed = _timing.CurTime - comp.PlaybackStartTime.Value;

        var playQuery = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (playQuery.MoveNext(out var receiver, out var receiverComp))
        {
            if (receiverComp.SoundEntity.HasValue)
                continue;

            RaiseLocalEvent(receiver, new StationRadioMediaPlayedEvent(comp.CurrentSong, elapsed));
        }
    }

    // Moffstation - Start - Alt click to lower volume.
    private static float GetGain(StationRadioReceiverComponent comp, bool powered)
    {
        if (!comp.Active || !powered)
            return 0f;

        return comp.LowVolume ? comp.LowVolumeGain : 1f;
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
