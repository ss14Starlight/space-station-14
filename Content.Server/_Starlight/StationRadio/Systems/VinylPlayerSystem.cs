using Content.Shared._Goobstation.StationRadio.Components;
using Content.Shared._Goobstation.StationRadio.Events;
using Content.Shared._Goobstation.StationRadio.Systems;
using Content.Shared.Destructible;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.StationRadio.Systems;

public sealed partial class VinylPlayerSystem : SharedVinylPlayerSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private StationRadioReceiverSystem _stationRadio = default!;
    [Dependency] private IGameTiming _timing = default!;

    protected override void OnPowerChanged(EntityUid uid, VinylPlayerComponent comp, PowerChangedEvent args)
    {
        if (args.Powered)
            return;

        if (comp.SoundEntity != null && !args.Powered)
            comp.SoundEntity = _audio.Stop(comp.SoundEntity);

        if (!_stationRadio.TryGetLinkedServer(uid, out var server) || !TryComp<StationRadioServerComponent>(server, out var serverComp)) // Starlight - Add Station Radio Resume Play
            return;

        serverComp.CurrentSong = null;
        serverComp.PlaybackStartTime = null;

        _stationRadio.StopAllReceivers(server);
    }

    protected override void OnDestruction(EntityUid uid, VinylPlayerComponent comp, DestructionEventArgs args)
    {
        if (!_stationRadio.TryGetLinkedServer(uid, out var server) || !TryComp<StationRadioServerComponent>(server, out var serverComp)) // Starlight - Add Station Radio Resume Play
            return;

        serverComp.CurrentSong = null;
        serverComp.PlaybackStartTime = null;

        var serverXform = Transform(uid);
        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out var _))
        {
            if (serverXform.GridUid != Transform(receiver).GridUid)
                continue;
            RaiseLocalEvent(receiver, new StationRadioMediaStoppedEvent());
        }
    }

    protected override void OnVinylInserted(EntityUid uid, VinylPlayerComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (!TryComp(args.Entity, out VinylComponent? vinylcomp) || vinylcomp.Song == null || !_power.IsPowered(uid))
            return;

        //var audio = _audio.PlayPvs(vinylcomp.Song, uid, comp.DefaultParams); // uhhhh vinyl can have playPVS i guess, sure :')
        //if (audio != null)
        //    comp.SoundEntity = audio.Value.Entity;

        // Used by VinylSummonRuleSystem
        var ev = new VinylInsertedEvent(args.Entity);
        RaiseLocalEvent(uid, ref ev);

        if (!_stationRadio.TryGetLinkedPoweredServer(uid, out var server) || !TryComp<StationRadioServerComponent>(server, out var serverComp)) // Starlight - Start - Add Station Radio Resume Play
            return;

        serverComp.CurrentSong = vinylcomp.Song;
        serverComp.PlaybackStartTime = _timing.CurTime;

        var serverXform = Transform(uid);
        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out _))
        {
            if (serverXform.GridUid != Transform(receiver).GridUid)
                continue;
            RaiseLocalEvent(receiver, new StationRadioMediaPlayedEvent(vinylcomp.Song, _timing.CurTime));
        }
    }

    protected override void OnVinylRemove(EntityUid uid, VinylPlayerComponent comp, EntRemovedFromContainerMessage args)
    {
        if (comp.SoundEntity != null)
            comp.SoundEntity = _audio.Stop(comp.SoundEntity);

        // Used by VinylSummonRuleSystem
        var ev = new VinylRemovedEvent(args.Entity);
        RaiseLocalEvent(uid, ref ev);

        if (!_stationRadio.TryGetPoweredGridServer(uid, out var server) || !TryComp<StationRadioServerComponent>(server, out var serverComp)) // Starlight - Start - Add Station Radio Resume Play
            return;

        // Starlight - Start - Add Station Radio Resume Play
        serverComp.CurrentSong = null;
        serverComp.PlaybackStartTime = null;
        // Starlight - End
        var serverXform = Transform(uid);
        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out var _))
        {
            if (serverXform.GridUid != Transform(receiver).GridUid)
                continue;
            RaiseLocalEvent(receiver, new StationRadioMediaStoppedEvent());
        }
    }
}
