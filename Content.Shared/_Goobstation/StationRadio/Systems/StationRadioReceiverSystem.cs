using Content.Shared._Goobstation.StationRadio.Components; // Starlight - _Goob -> _Goobstation
using Content.Shared._Goobstation.StationRadio.Events; // Starlight - _Goob -> _Goobstation
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Systems;
using Content.Shared.DeviceLinking; // Starlight - Remove Server Check from VinylSummonSystem
using Content.Shared.Examine; // Starlight - Shift Click to view what volume the radio is at.
using Content.Shared.Verbs; // Starlight - Alt click to lower volume.
using Robust.Shared.Network; // Starlight - Add Station Radio Resume Play
using Robust.Shared.Timing; // Starlight - Add Station Radio Resume Play

namespace Content.Shared._Goobstation.StationRadio.Systems; // Starlight - _Goob -> _Goobstation

public sealed partial class StationRadioReceiverSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    // Starlight - Add Station Radio Resume Play
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    // Starlight - End
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaPlayedEvent>(OnMediaPlayed);
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaStoppedEvent>(OnMediaStopped);
        SubscribeLocalEvent<StationRadioReceiverComponent, ActivateInWorldEvent>(OnRadioToggle);
        SubscribeLocalEvent<StationRadioReceiverComponent, PowerChangedEvent>(OnPowerChanged);

        SubscribeLocalEvent<StationRadioReceiverComponent, MapInitEvent>(OnReceiverMapInit); // Starlight - Add Radio Resume Play

        SubscribeLocalEvent<StationRadioServerComponent, PowerChangedEvent>(OnServerPowerChanged); // Starlight - Fix Server Broadcasting Music with no power.
        SubscribeLocalEvent<StationRadioServerComponent, EntityTerminatingEvent>(OnServerTerminating); // Starlight - When Server is destroyed, it should stop broadcasting.
        SubscribeLocalEvent<RadioRigComponent, EntityTerminatingEvent>(OnRigTerminating); // Starlight - When Rig is destroyed, it should stop broadcasting.

        SubscribeLocalEvent<StationRadioReceiverComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs); // Starlight - Alt click to lower volume.
        SubscribeLocalEvent<StationRadioReceiverComponent, ExaminedEvent>(OnExamined); // Starlight - Shift Click to view what volume the radio is at.
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

        if (!TryGetLinkedPoweredServer(uid, out var server) || !TryComp<StationRadioServerComponent>(server, out var serverComp))
            return;

        if (serverComp.CurrentSong == null || serverComp.PlaybackStartTime == null)
            return;

        var elapsed = _timing.CurTime - serverComp.PlaybackStartTime.Value;
        RaiseLocalEvent(uid, new StationRadioMediaPlayedEvent(serverComp.CurrentSong, elapsed));
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
                if (!TryGetLinkedServer(receiver, out var linkedServer) || linkedServer != uid)
                    continue;

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

            if (!TryGetLinkedServer(receiver, out var linkedServer) || linkedServer != uid)
                continue;

            RaiseLocalEvent(receiver, new StationRadioMediaPlayedEvent(comp.CurrentSong, elapsed));
        }
    }

    // Starlight - Start
    #region Starlight
    /// <summary>
    /// Method for getting the current volume of the station radio.
    /// </summary>
    private static float GetGain(StationRadioReceiverComponent comp, bool powered)
    {
        if (!comp.Active || !powered)
            return 0f;

        return comp.LowVolume ? comp.LowVolumeGain : 1f;
    }

    /// <summary>
    /// Alt Click / Context Menu Verb for turning down the volume of the radio.
    /// </summary>
    private void OnGetAltVerbs(EntityUid uid, StationRadioReceiverComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = comp.LowVolume ? "Increase Volume" : "Decrease Volume",
            Act = () =>
            {
                comp.LowVolume = !comp.LowVolume;
                Dirty(uid, comp);
                if (comp.SoundEntity != null)
                    _audio.SetGain(comp.SoundEntity, GetGain(comp, _power.IsPowered(uid)));
            }
        });
    }

    /// <summary>
    /// Resolves whether a Radio Rig is linked to a Radio Server.
    /// </summary>
    public bool TryGetLinkedServer(EntityUid uid, out EntityUid server)
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
                if (!HasComp<StationRadioServerComponent>(linkedServer))
                continue;

                server = linkedServer;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Resolves whether Radio Rig is linked to a Radio Server that is powered and whether or not it can broadcast.
    /// </summary>
    public bool TryGetLinkedPoweredServer(EntityUid uid, out EntityUid server)
    {
        return TryGetLinkedServer(uid, out server) && _power.IsPowered(server);
    }

    /// <summary>
    /// Stop broadcasting if the Radio Server loses power, despite the Vinyl Player and Rig still being powered.
    /// </summary>
    private void OnPowerChanged(EntityUid uid, StationRadioReceiverComponent comp, PowerChangedEvent args)
    {
        if(comp.SoundEntity == null)
            return;
        _audio.SetGain(comp.SoundEntity, GetGain(comp, args.Powered));
    }

    /// <summary>
    /// When the Radio Server is destroyed, stop all station radio receivers.
    /// </summary>
    private void OnServerTerminating(EntityUid uid, StationRadioServerComponent comp, ref EntityTerminatingEvent args) => StopAllReceivers();

    /// <summary>
    /// When the Radio Rig is destroyed, stop all station radio receivers.
    /// </summary>
    private void OnRigTerminating(EntityUid uid, RadioRigComponent comp, ref EntityTerminatingEvent args) => StopAllReceivers();

    /// <summary>
    /// Stop the broadcast on server destruction.
    /// </summary>
    private void StopAllReceivers()
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out _))
        {
            RaiseLocalEvent(receiver, new StationRadioMediaStoppedEvent());
        }
    }

    /// <summary>
    /// Display whether or not the station radio is at full or low volume when examined.
    /// </summary>
    private void OnExamined(EntityUid uid, StationRadioReceiverComponent comp, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(comp.LowVolume
            ? "station-radio-receiver-examine-low-volume"
            : "station-radio-receiver-examine-full-volume"));
    }
    #endregion
    // Starlight - End
}
