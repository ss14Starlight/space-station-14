using Content.Shared._Goobstation.StationRadio.Components; // Starlight - _Goob -> _Goobstation
using Content.Shared._Goobstation.StationRadio.Events; // Starlight - _Goob -> _Goobstation
using Content.Shared.Destructible;
using Content.Shared.DeviceLinking;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Content.Shared.Examine; // Starlight - Shift Click to view what Vinyl is inserted.
using Robust.Shared.Timing; // Starlight - Add Station Radio Resume Play

namespace Content.Shared._Goobstation.StationRadio.Systems; // Starlight - _Goob -> _Goobstation

public sealed partial class VinylPlayerSystem : EntitySystem
{

    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private StationRadioReceiverSystem _stationRadio = default!; // Starlight - Remove Server Check from VinylSummonSystem
    [Dependency] private IGameTiming _timing = default!; // Starlight - Add Station Radio Resume Play

    [Dependency] private readonly SharedContainerSystem _container = default!; // Starlight - Shift Click to view what Vinyl is inserted.

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VinylPlayerComponent, EntInsertedIntoContainerMessage>(OnVinylInserted);
        SubscribeLocalEvent<VinylPlayerComponent, EntRemovedFromContainerMessage>(OnVinylRemove);
        SubscribeLocalEvent<VinylPlayerComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<VinylPlayerComponent, PowerChangedEvent>(OnPowerChanged);

        SubscribeLocalEvent<VinylPlayerComponent, ExaminedEvent>(OnExamined); // Starlight - Shift Click to view what Vinyl is inserted.
    }

    private void OnPowerChanged(EntityUid uid, VinylPlayerComponent comp, PowerChangedEvent args)
    {
        if (comp.SoundEntity != null && !args.Powered)
            comp.SoundEntity = _audio.Stop(comp.SoundEntity);

        if (!_stationRadio.TryGetLinkedPoweredServer(uid, out var server) || !TryComp<StationRadioServerComponent>(server, out var serverComp)) // Starlight - Add Station Radio Resume Play
            return;

        // Starlight - Start - Add Station Radio Resume Play
        serverComp.CurrentSong = null;
        serverComp.PlaybackStartTime = null;
        Dirty(server, serverComp);
        // Starlight - End

        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out _))
        {
            RaiseLocalEvent(receiver, new StationRadioMediaStoppedEvent());
        }
    }

    private void OnDestruction(EntityUid uid, VinylPlayerComponent comp, DestructionEventArgs args)
    {
        if (!_stationRadio.TryGetLinkedPoweredServer(uid, out var server) || !TryComp<StationRadioServerComponent>(server, out var serverComp)) // Starlight - Add Station Radio Resume Play
            return;

        // Starlight - Start - Add Station Radio Resume Play
        serverComp.CurrentSong = null;
        serverComp.PlaybackStartTime = null;
        Dirty(server, serverComp);
        // Starlight - End

        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out var _))
        {
            RaiseLocalEvent(receiver, new StationRadioMediaStoppedEvent());
        }
    }

    private void OnVinylInserted(EntityUid uid, VinylPlayerComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (!TryComp(args.Entity, out VinylComponent? vinylcomp) || _net.IsClient || vinylcomp.Song == null || !_power.IsPowered(uid))
            return;

        var audio = _audio.PlayPredicted(vinylcomp.Song, uid, uid, comp.DefaultParams); // Starlight - Dehardcode Params
        if (audio != null)
            comp.SoundEntity = audio.Value.Entity;

        // Used by VinylSummonRuleSystem
        var ev = new VinylInsertedEvent(args.Entity);
        RaiseLocalEvent(uid, ref ev);

        if (!_stationRadio.TryGetLinkedPoweredServer(uid, out var server) || !TryComp<StationRadioServerComponent>(server, out var serverComp)) // Starlight - Start - Add Station Radio Resume Play
            return;

        // Starlight - Start - Add Station Radio Resume Play
        serverComp.CurrentSong = vinylcomp.Song;
        serverComp.PlaybackStartTime = _timing.CurTime;
        Dirty(server, serverComp);
        // Starlight - End

        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out var receiverComponent))
        {
            if (!receiverComponent.SoundEntity.HasValue)
                RaiseLocalEvent(receiver, new StationRadioMediaPlayedEvent(vinylcomp.Song));
        }
    }

    private void OnVinylRemove(EntityUid uid, VinylPlayerComponent comp, EntRemovedFromContainerMessage args)
    {
        if (comp.SoundEntity != null)
            comp.SoundEntity = _audio.Stop(comp.SoundEntity);

        // Used by VinylSummonRuleSystem
        var ev = new VinylRemovedEvent(args.Entity);
        RaiseLocalEvent(uid, ref ev);

        if (!_stationRadio.TryGetLinkedPoweredServer(uid, out var server) || !TryComp<StationRadioServerComponent>(server, out var serverComp)) // Starlight - Start - Add Station Radio Resume Play
            return;

        // Starlight - Start - Add Station Radio Resume Play
        serverComp.CurrentSong = null;
        serverComp.PlaybackStartTime = null;
        Dirty(server, serverComp);
        // Starlight - End

        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out var _))
        {
            RaiseLocalEvent(receiver, new StationRadioMediaStoppedEvent());
        }
    }

    private bool CheckForRadioRig(EntityUid uid)
    {
        if (TryComp<DeviceLinkSourceComponent>(uid, out var source))
        {
            foreach (var linked in source.LinkedPorts.Keys)
            {
                if (HasComp<RadioRigComponent>(linked) && CheckForRadioServer(linked))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private bool CheckForRadioServer(EntityUid uid)
    {
        if (TryComp<DeviceLinkSinkComponent>(uid, out var source))
        {
            foreach (var linked in source.LinkedSources)
            {
                if (HasComp<StationRadioServerComponent>(linked))
                {
                    return true;
                }
            }
        }
        return false;
    }

    #region Starlight
    /// <summary>
    /// Show what vinyl is currently inserted when examined.
    /// </summary>
    private void OnExamined(EntityUid uid, VinylPlayerComponent comp, ref ExaminedEvent args)
    {
        if (!_container.TryGetContainer(uid, "vinyl", out var container) || container.ContainedEntities.Count == 0) // confirm actual container ID
        {
            args.PushMarkup(Loc.GetString("vinyl-player-examine-empty"));
            return;
        }

        var vinyl = container.ContainedEntities[0];
        args.PushMarkup(Loc.GetString("vinyl-player-examine-loaded", ("vinyl", Name(vinyl))));
    }
    #endregion
}
