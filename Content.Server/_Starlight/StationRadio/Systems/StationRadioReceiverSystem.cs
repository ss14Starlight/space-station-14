using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Starlight.StationRadio;
using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared._Starlight.StationRadio.Events;
using Content.Shared._Starlight.StationRadio.Systems;
using Content.Shared.DeviceLinking;
using Content.Shared.Radio.Components;
using Content.Shared.Radio.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.StationRadio.Systems;

public sealed partial class StationRadioReceiverSystem: SharedStationRadioReceiverSystem
{

    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedRadioDeviceSystem _radioDevice = default!;

    protected override void OnReceiverMapInit(EntityUid uid, StationRadioReceiverComponent comp, MapInitEvent args)
    {
        if (!TryGetPoweredGridServer(uid, out var server) || !TryComp<StationRadioServerComponent>(server, out var serverComp))
            return;

        comp.Tracks = serverComp.MusicMuted ? new Dictionary<NetEntity, StationRadioTrack>() : GetNetTracks(serverComp);
        comp.Interference = serverComp.Interference;
        Dirty(uid, comp);
    }

    protected override void OnServerPowerChanged(EntityUid uid, StationRadioServerComponent comp, PowerChangedEvent args)
    {
        if (Transform(uid).Anchored)
            SyncReceivers((uid, comp));
    }

    protected override void OnServerTerminating(EntityUid uid, StationRadioServerComponent comp,
        ref EntityTerminatingEvent args)
    {
        if (Transform(uid).Anchored)
            StopAllReceivers(uid);
    }

    protected override void OnRigTerminating(EntityUid uid, RadioRigComponent comp, ref EntityTerminatingEvent args)
    {
        if (TryGetLinkedPoweredServer(uid, out var linkedServer) && TryComp<StationRadioServerComponent>(linkedServer, out var serverComp))
        {
            serverComp.Tracks.Clear();
            SyncReceivers((linkedServer, serverComp));
        }
    }

    [SubscribeLocalEvent]
    private void OnVolumeMessage(EntityUid uid, StationRadioReceiverComponent comp, StationRadioReceiverVolumeMessage args) =>
        SetVolume((uid, comp), args.Volume);

    [SubscribeLocalEvent]
    private void OnPowerMessage(EntityUid uid, StationRadioReceiverComponent comp, StationRadioReceiverPowerMessage args)
    {
        comp.Active = !comp.Active;
        Dirty(uid, comp);

        if (HasComp<RadioSpeakerComponent>(uid))
            _radioDevice.SetSpeakerEnabled(uid, args.Actor, comp.Active, quiet: true);
    }

    /// <summary>
    /// Turns the radio's own volume knob.
    /// </summary>
    public void SetVolume(Entity<StationRadioReceiverComponent> ent, float volume)
    {
        ent.Comp.Volume = Math.Clamp(volume, 0f, 1f);
        Dirty(ent);
    }

    /// <summary>
    /// Starts broadcasting a sound from the given source, replacing whatever that source played before.
    /// </summary>
    public void PlayTrack(Entity<StationRadioServerComponent> server, EntityUid source, SoundPathSpecifier sound, TimeSpan? startTime = null)
    {
        server.Comp.Tracks[source] = new StationRadioTrack(sound, startTime ?? _timing.CurTime);
        SyncReceivers(server);
    }

    /// <summary>
    /// Stops whatever the source broadcasts, on any server.
    /// </summary>
    public void StopTrack(EntityUid source)
    {
        var query = EntityQueryEnumerator<StationRadioServerComponent>();
        while (query.MoveNext(out var server, out var serverComp))
        {
            if (serverComp.Tracks.Remove(source))
                SyncReceivers((server, serverComp));
        }
    }

    /// <summary>
    /// Pushes the server's tracks to every receiver on its grid, or silences them if the server can't broadcast.
    /// </summary>
    public void SyncReceivers(Entity<StationRadioServerComponent> server)
    {
        var serverXform = Transform(server);
        var broadcasting = serverXform.Anchored && _power.IsPowered(server.Owner);
        var tracks = broadcasting && !server.Comp.MusicMuted
            ? GetNetTracks(server.Comp)
            : new Dictionary<NetEntity, StationRadioTrack>();
        var interference = broadcasting ? server.Comp.Interference : 0f;

        var query = EntityQueryEnumerator<StationRadioReceiverComponent, TransformComponent>();
        while (query.MoveNext(out var receiver, out var receiverComp, out var receiverXform))
        {
            if (receiverXform.GridUid != serverXform.GridUid)
                continue;

            SetTracks((receiver, receiverComp), tracks, interference);
        }

        var ev = new StationRadioTracksChangedEvent();
        RaiseLocalEvent(server, ref ev);
    }

    public void StopAllReceivers(EntityUid uid)
    {
        var serverXform = Transform(uid);

        var stopQuery = EntityQueryEnumerator<StationRadioReceiverComponent, TransformComponent>();
        while (stopQuery.MoveNext(out var receiver, out var receiverComp, out var receiverXform))
        {
            if(receiverXform.GridUid != serverXform.GridUid)
                continue;

            SetTracks((receiver, receiverComp), new Dictionary<NetEntity, StationRadioTrack>(), 0f);
        }
    }

    private void SetTracks(Entity<StationRadioReceiverComponent> receiver, Dictionary<NetEntity, StationRadioTrack> tracks, float interference)
    {
        if (receiver.Comp.Tracks.Count == tracks.Count
            && !tracks.Except(receiver.Comp.Tracks).Any()
            && MathHelper.CloseTo(receiver.Comp.Interference, interference))
            return;

        receiver.Comp.Interference = interference;
        receiver.Comp.Tracks = new Dictionary<NetEntity, StationRadioTrack>(tracks);
        Dirty(receiver);
    }

    private Dictionary<NetEntity, StationRadioTrack> GetNetTracks(StationRadioServerComponent comp) =>
        comp.Tracks
            .Where(pair => Exists(pair.Key))
            .ToDictionary(pair => GetNetEntity(pair.Key), pair => pair.Value with { Gain = GetTrackGain(comp, pair.Key) * comp.MasterGain });

    /// <summary>
    /// Mixer volume of a broadcast source, full volume unless the mixer turned it down.
    /// </summary>
    public static float GetSourceGain(StationRadioServerComponent comp, EntityUid source) =>
        comp.SourceGains.GetValueOrDefault(source, 1f);

    /// <summary>
    /// Mixer volume of a track before the master fader: its source's fader, and for a recorder playing a tape
    /// also the recorder's own fader, so a whole replay can be turned down at once.
    /// </summary>
    public float GetTrackGain(StationRadioServerComponent comp, EntityUid source)
    {
        var gain = GetSourceGain(comp, source);
        if (TryComp<BroadcastRecorderChannelComponent>(source, out var channel))
            gain *= GetSourceGain(comp, channel.Recorder);

        return gain;
    }

    /// <summary>
    /// Resolves whether Radio Rig is linked to a Radio Server that is powered and whether it can broadcast.
    /// </summary>
    public bool TryGetPoweredGridServer(EntityUid uid, [NotNullWhen(true)]out EntityUid? server) =>
        TryGetGridServer(uid, out server) && _power.IsPowered(server.Value);

    /// <summary>
    /// Resolves the current radio server on the entities grid
    /// </summary>
    public bool TryGetGridServer(EntityUid uid, [NotNullWhen(true)]out EntityUid? server)
    {
        server = null;

        var receiverXform = Transform(uid);
        var query = EntityQueryEnumerator<StationRadioServerComponent>();

        while (query.MoveNext(out var serverUid, out var _))
        {
            var serverXform =  Transform(serverUid);
            if (serverXform.GridUid != receiverXform.GridUid || !serverXform.Anchored)
                continue;
            server = serverUid;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Resolves whether the entity(vinyl player or rig) is linked to a Radio Server.
    /// </summary>
    public bool TryGetLinkedPoweredServer(EntityUid uid, out EntityUid server)
    {
        server = default;

        // The rig plays its own record shelf through the server feeding it.
        if (HasComp<RadioRigComponent>(uid) && TryComp<DeviceLinkSinkComponent>(uid, out var rigSink))
        {
            foreach (var linkedServer in rigSink.LinkedSources)
            {
                if (!HasComp<StationRadioServerComponent>(linkedServer) || !_power.IsPowered(linkedServer))
                    continue;

                server = linkedServer;
                return true;
            }

            return false;
        }

        if (!TryComp<DeviceLinkSourceComponent>(uid, out var source))
            return false;

        foreach (var linked in source.LinkedPorts.Keys.Where(linked => _power.IsPowered(linked)))
        {
            if (HasComp<StationRadioServerComponent>(linked))
            {
                server = linked;
                return true;
            }

            if (!HasComp<RadioRigComponent>(linked) || !TryComp<DeviceLinkSinkComponent>(linked, out var sink))
                continue;

            foreach (var linkedServer in sink.LinkedSources.Where(linkedServer => HasComp<StationRadioServerComponent>(linkedServer) && _power.IsPowered(linkedServer)))
            {
                server = linkedServer;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Resolves whether the entity(vinyl player or rig) is linked to a Radio Server.
    /// </summary>
    public bool TryGetLinkedServer(EntityUid uid, out EntityUid server)
    {
        server = default;

        if (HasComp<RadioRigComponent>(uid) && TryComp<DeviceLinkSinkComponent>(uid, out var rigSink))
        {
            foreach (var linkedServer in rigSink.LinkedSources)
            {
                if (!HasComp<StationRadioServerComponent>(linkedServer))
                    continue;

                server = linkedServer;
                return true;
            }

            return false;
        }

        if (!TryComp<DeviceLinkSourceComponent>(uid, out var source))
            return false;

        foreach (var linked in source.LinkedPorts.Keys)
        {
            if (HasComp<StationRadioServerComponent>(linked))
            {
                server = linked;
                return true;
            }

            if (!HasComp<RadioRigComponent>(linked) || !TryComp<DeviceLinkSinkComponent>(linked, out var sink))
                continue;

            foreach (var linkedServer in sink.LinkedSources.Where(HasComp<StationRadioServerComponent>))
            {
                server = linkedServer;
                return true;
            }
        }
        return false;
    }

    protected override void OnAttemptAnchor(EntityUid uid, StationRadioServerComponent comp, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
        {
            if (TryGetGridServer(uid, out var other) && TryComp<StationRadioServerComponent>(other, out var otherComp))
                SyncReceivers((other.Value, otherComp));
            else
                StopAllReceivers(uid);
            return;
        }

        var xform = Transform(uid);
        var c = 0;
        var query = EntityQueryEnumerator<StationRadioServerComponent>();
        foreach (var ent in  query)
        {
            var entXform = Transform(ent);
            if (entXform.Anchored && entXform.GridUid == xform.GridUid)
                c++;

            if (c > 1)
            {
                _transform.Unanchor(uid);
                _popupSystem.PopupEntity(Loc.GetString("station-radio-server-cancelled-anchor"), uid, PopupType.Medium);
                return;
            }
        }

        SyncReceivers((uid, comp));
    }
}
