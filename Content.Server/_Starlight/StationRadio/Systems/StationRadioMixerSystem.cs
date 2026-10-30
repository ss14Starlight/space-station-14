using Content.Shared._Starlight.StationRadio;
using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared._Starlight.StationRadio.Events;
using Content.Shared.Power.EntitySystems;

namespace Content.Server._Starlight.StationRadio.Systems;

public sealed partial class StationRadioMixerSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private StationRadioReceiverSystem _stationRadio = default!;
    [Dependency] private VinylPlayerSystem _vinylPlayer = default!;
    [Dependency] private BroadcastRecorderSystem _recorder = default!;

    [SubscribeLocalEvent]
    private void OnUiOpened(EntityUid uid, StationRadioMixerComponent comp, BoundUIOpenedEvent args) =>
        UpdateUi((uid, comp));

    [SubscribeLocalEvent]
    private void OnSetGain(EntityUid uid, StationRadioMixerComponent comp, StationRadioMixerSetGainMessage args)
    {
        if (comp.RequiresPower && !_power.IsPowered(uid))
            return;

        if (!_stationRadio.TryGetLinkedPoweredServer(uid, out var server)
            || !TryComp<StationRadioServerComponent>(server, out var serverComp))
            return;

        var gain = Math.Clamp(args.Gain, 0f, 1f);
        switch (args.Fader)
        {
            case StationRadioMixerFader.Source:
                if (GetEntity(args.Source) is not { } source
                    || !_stationRadio.TryGetLinkedServer(source, out var linked)
                    || linked != server)
                    return;

                serverComp.SourceGains[source] = gain;
                break;
            case StationRadioMixerFader.Master:
                serverComp.MasterGain = gain;
                break;
            case StationRadioMixerFader.Voice:
                serverComp.VoiceGain = gain;
                break;
            case StationRadioMixerFader.Monitor:
                if (TryComp<StationRadioReceiverComponent>(uid, out var monitor))
                    _stationRadio.SetVolume((uid, monitor), gain);
                break;
        }

        _stationRadio.SyncReceivers((server, serverComp));
    }

    [SubscribeLocalEvent]
    private void OnTransport(EntityUid uid, StationRadioMixerComponent comp, StationRadioMixerTransportMessage args)
    {
        if (comp.RequiresPower && !_power.IsPowered(uid))
            return;

        if (!_stationRadio.TryGetLinkedPoweredServer(uid, out var server)
            || !TryGetEntity(args.Source, out var source)
            || !_stationRadio.TryGetLinkedServer(source.Value, out var linked)
            || linked != server)
            return;

        if (TryComp<BroadcastRecorderComponent>(source, out var recorder))
        {
            if (args.Action == StationRadioMixerAction.TogglePlay)
                _recorder.TogglePlayback((source.Value, recorder), args.Actor);
            else if (args.Action == StationRadioMixerAction.Rewind)
                _recorder.Rewind((source.Value, recorder), args.Actor);
            return;
        }

        if (!TryComp<VinylPlayerComponent>(source, out var player))
            return;

        switch (args.Action)
        {
            case StationRadioMixerAction.TogglePlay:
                _vinylPlayer.TogglePlay((source.Value, player), args.Actor);
                break;
            case StationRadioMixerAction.Rewind:
                _vinylPlayer.Rewind((source.Value, player), args.Actor);
                break;
            case StationRadioMixerAction.Next:
                _vinylPlayer.Next((source.Value, player), args.Actor);
                break;
            case StationRadioMixerAction.Select:
                _vinylPlayer.SelectSlot((source.Value, player), args.Slot, args.Actor);
                break;
            case StationRadioMixerAction.Eject:
                _vinylPlayer.EjectSlot((source.Value, player), args.Slot, args.Actor);
                break;
        }
    }

    [SubscribeLocalEvent]
    private void OnTracksChanged(Entity<StationRadioServerComponent> ent, ref StationRadioTracksChangedEvent args) =>
        UpdateMixers(ent);

    [SubscribeLocalEvent]
    private void OnPlayerStateChanged(Entity<VinylPlayerComponent> ent, ref VinylPlayerStateChangedEvent args)
    {
        if (_stationRadio.TryGetLinkedServer(ent, out var server))
            UpdateMixers(server);
    }

    private void UpdateMixers(EntityUid server)
    {
        var query = EntityQueryEnumerator<StationRadioMixerComponent>();
        while (query.MoveNext(out var mixer, out var mixerComp))
        {
            if (_ui.IsUiOpen(mixer, StationRadioMixerUiKey.Key)
                && _stationRadio.TryGetLinkedServer(mixer, out var linked)
                && linked == server)
                UpdateUi((mixer, mixerComp));
        }
    }

    private void UpdateUi(Entity<StationRadioMixerComponent> ent)
    {
        var channels = new List<StationRadioMixerChannel>();
        if (!_stationRadio.TryGetLinkedPoweredServer(ent, out var server)
            || !TryComp<StationRadioServerComponent>(server, out var serverComp))
        {
            _ui.SetUiState(ent.Owner, StationRadioMixerUiKey.Key, new StationRadioMixerState(channels, 1f, 1f, false));
            return;
        }

        var query = EntityQueryEnumerator<VinylPlayerComponent>();
        while (query.MoveNext(out var source, out var player))
        {
            if (player.Standalone || !_stationRadio.TryGetLinkedServer(source, out var linked) || linked != server)
                continue;

            var disc = _vinylPlayer.GetCurrentDisc((source, player));
            var slots = new List<string?>();
            for (var i = 0; i < player.Slots.Length; i++)
            {
                var slotDisc = _vinylPlayer.GetDisc((source, player), i);
                slots.Add(slotDisc != null ? Name(slotDisc.Value) : null);
            }

            channels.Add(new StationRadioMixerChannel(
                GetNetEntity(source),
                player.ChannelName is { } channelName ? Loc.GetString(channelName) : Name(source),
                StationRadioReceiverSystem.GetSourceGain(serverComp, source),
                player.Playing,
                disc != null ? Name(disc.Value) : null,
                slots,
                player.CurrentSlot,
                _vinylPlayer.GetLength((source, player)),
                player.Position,
                player.PlaybackStart));
        }

        var recorders = EntityQueryEnumerator<BroadcastRecorderComponent>();
        while (recorders.MoveNext(out var recorder, out var recorderComp))
        {
            if (!_stationRadio.TryGetLinkedServer(recorder, out var linked) || linked != server)
                continue;

            var tapeName = _recorder.GetTapeName((recorder, recorderComp));
            var tape = _recorder.GetTape((recorder, recorderComp));
            var slots = new List<string?>();
            slots.Add(tapeName);
            channels.Add(new StationRadioMixerChannel(
                GetNetEntity(recorder),
                Name(recorder),
                StationRadioReceiverSystem.GetSourceGain(serverComp, recorder),
                recorderComp.Mode == BroadcastRecorderMode.Playing,
                tapeName,
                slots,
                0,
                tape?.Length ?? TimeSpan.Zero,
                recorderComp.Position,
                recorderComp.Start));
        }

        channels.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        _ui.SetUiState(ent.Owner, StationRadioMixerUiKey.Key,
            new StationRadioMixerState(channels, serverComp.MasterGain, serverComp.VoiceGain, true,
                serverComp.Listeners, serverComp.Interference,
                CompOrNull<StationRadioReceiverComponent>(ent)?.Volume));
    }
}
