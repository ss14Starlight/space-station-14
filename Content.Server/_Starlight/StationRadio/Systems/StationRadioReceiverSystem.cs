using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Goobstation.StationRadio.Components;
using Content.Shared._Goobstation.StationRadio.Events;
using Content.Shared._Goobstation.StationRadio.Systems;
using Content.Shared.DeviceLinking;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.StationRadio.Systems;

public sealed partial class StationRadioReceiverSystem: SharedStationRadioReceiverSystem
{

    [Dependency] private SharedPowerReceiverSystem _power = default!;

    /// <summary>
    /// When a station radio is initialized, check any active Radio server for if there is an
    /// active song playing. If there is, attempt to resume play.
    /// </summary>
    protected override void OnReceiverMapInit(EntityUid uid, StationRadioReceiverComponent comp, MapInitEvent args)
    {
        if (!TryGetPoweredGridServer(uid, out var server) || !TryComp<StationRadioServerComponent>(server, out var serverComp))
            return;

        if (serverComp.CurrentSong == null || serverComp.PlaybackStartTime == null)
            return;

        comp.CurrentSound = serverComp.CurrentSong;
        comp.StartTime = serverComp.PlaybackStartTime.Value;
        Dirty(uid, comp);
    }

    /// <summary>
    /// Stop broadcasting if the Radio Server loses power, despite the Vinyl Player and Rig still being powered.
    /// Resume play when the server power returns.
    /// </summary>
    protected override void OnServerPowerChanged(EntityUid uid, StationRadioServerComponent comp, PowerChangedEvent args)
    {
        if (!args.Powered)
        {
            StopAllReceivers(uid);
            return;
        }

        if (comp.CurrentSong == null || comp.PlaybackStartTime == null)
            return;

        var playQuery = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (playQuery.MoveNext(out var receiver, out var receiverComp))
        {
            if (receiverComp.SoundEntity.HasValue)
                continue;

            if (!TryGetGridServer(receiver, out var linkedServer) || linkedServer != uid)
                continue;

            RaiseLocalEvent(receiver, new StationRadioMediaPlayedEvent(comp.CurrentSong, comp.PlaybackStartTime.Value));
        }
    }

    protected override void OnMediaPlayed(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaPlayedEvent args)
    {
        comp.CurrentSound = args.MediaPlayed;
        comp.StartTime = args.StartTime;
        Dirty(uid, comp);
    }

    protected override void OnMediaStopped(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaStoppedEvent args)
    {
        comp.CurrentSound = null;
        comp.StartTime = null;
        Dirty(uid, comp);
    }

    protected override void OnServerTerminating(EntityUid uid, StationRadioServerComponent comp,
        ref EntityTerminatingEvent args) => StopAllReceivers(uid);

    protected override void OnRigTerminating(EntityUid uid, RadioRigComponent comp, ref EntityTerminatingEvent args)
    {
        if (TryGetLinkedPoweredServer(uid, out var linkedServer))
            StopAllReceivers(linkedServer);
    }


    public void StopAllReceivers(EntityUid uid)
    {
        var serverXform = Transform(uid);

        var stopQuery = EntityQueryEnumerator<StationRadioReceiverComponent, TransformComponent>();
        while (stopQuery.MoveNext(out var receiver, out _, out var receiverXform))
        {
            if(receiverXform.GridUid != serverXform.GridUid)
                continue;

            RaiseLocalEvent(receiver, new StationRadioMediaStoppedEvent());
        }
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
            if (serverXform.GridUid != receiverXform.GridUid)
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
}
