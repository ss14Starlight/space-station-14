using System.Linq;
using Content.Server._Starlight.TextToSpeech;
using Content.Server.Radio.EntitySystems;
using Content.Server.Speech.Components;
using Content.Shared._Starlight.Language;
using Content.Shared._Starlight.StationRadio;
using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared._Starlight.TextToSpeech;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.StationRadio.Systems;

public sealed partial class BroadcastRecorderSystem : EntitySystem
{
    private static readonly TimeSpan GainMergeWindow = TimeSpan.FromSeconds(0.25);

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private StationRadioReceiverSystem _stationRadio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadioSpokeEvent>(OnRadioSpoke);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BroadcastRecorderComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Mode == BroadcastRecorderMode.Stopped)
                continue;

            if (GetTape((uid, comp)) is not { } tape
                || !_stationRadio.TryGetLinkedPoweredServer(uid, out var server)
                || !TryComp<StationRadioServerComponent>(server, out var serverComp))
            {
                _popup.PopupEntity(Loc.GetString("broadcast-recorder-popup-no-server"), uid, PopupType.Medium);
                Stop((uid, comp));
                continue;
            }

            if (comp.Mode == BroadcastRecorderMode.Recording)
                UpdateRecording((uid, comp), tape, (server, serverComp));
            else
                UpdatePlayback((uid, comp), tape, (server, serverComp));
        }
    }

    #region Interaction

    [SubscribeLocalEvent]
    private void OnUiOpened(EntityUid uid, BroadcastRecorderComponent comp, BoundUIOpenedEvent args) =>
        UpdateUi((uid, comp));

    [SubscribeLocalEvent]
    private void OnAction(EntityUid uid, BroadcastRecorderComponent comp, BroadcastRecorderActionMessage args)
    {
        var ent = (uid, comp);
        switch (args.Action)
        {
            case BroadcastRecorderAction.Record:
                StartRecording(ent, args.Actor);
                break;
            case BroadcastRecorderAction.Play:
                StartPlayback(ent, args.Actor);
                break;
            case BroadcastRecorderAction.Stop:
                Stop(ent);
                break;
            case BroadcastRecorderAction.Rewind:
                Rewind(ent, args.Actor);
                break;
        }
    }

    [SubscribeLocalEvent]
    private void OnTapeInserted(EntityUid uid, BroadcastRecorderComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != comp.Slot)
            return;

        comp.Position = TimeSpan.Zero;
        UpdateVisuals((uid, comp));
        UpdateUi((uid, comp));
    }

    [SubscribeLocalEvent]
    private void OnTapeRemoved(EntityUid uid, BroadcastRecorderComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != comp.Slot)
            return;

        Stop((uid, comp), args.Entity);
        comp.Position = TimeSpan.Zero;
        UpdateVisuals((uid, comp));
        UpdateUi((uid, comp));
    }

    [SubscribeLocalEvent]
    private void OnPowerChanged(EntityUid uid, BroadcastRecorderComponent comp, PowerChangedEvent args)
    {
        if (!args.Powered)
            Stop((uid, comp));
    }

    [SubscribeLocalEvent]
    private void OnTapeExamined(EntityUid uid, BroadcastTapeComponent comp, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("broadcast-tape-examine",
            ("recorded", FormatTime(comp.Length)),
            ("max", FormatTime(comp.MaxLength))));
    }

    #endregion

    #region Recording

    public void StartRecording(Entity<BroadcastRecorderComponent> ent, EntityUid? user = null)
    {
        if (ent.Comp.Mode != BroadcastRecorderMode.Stopped)
            Stop(ent);

        if (!CanRun(ent, user, out var tape, out _))
            return;

        var position = ent.Comp.Position >= tape.MaxLength ? TimeSpan.Zero : ent.Comp.Position;
        tape.Events.RemoveAll(e => e.Time >= position);
        tape.Length = position;

        ent.Comp.Position = position;
        ent.Comp.Start = _timing.CurTime - position;
        ent.Comp.RecordChannels.Clear();
        ent.Comp.RecordState.Clear();
        ent.Comp.Mode = BroadcastRecorderMode.Recording;
        UpdateVisuals(ent);
        UpdateUi(ent);
    }

    private void UpdateRecording(
        Entity<BroadcastRecorderComponent> ent,
        BroadcastTapeComponent tape,
        Entity<StationRadioServerComponent> server)
    {
        var comp = ent.Comp;
        var now = _timing.CurTime;
        var position = now - comp.Start;
        if (position >= tape.MaxLength)
        {
            _popup.PopupEntity(Loc.GetString("broadcast-recorder-popup-tape-full"), ent, PopupType.Medium);
            Stop(ent);
            return;
        }

        foreach (var (source, track) in server.Comp.Tracks)
        {
            if (!Exists(source))
                continue;

            var gain = StationRadioReceiverSystem.GetSourceGain(server.Comp, source);
            if (!comp.RecordChannels.TryGetValue(source, out var channel))
                comp.RecordChannels[source] = channel = comp.RecordChannels.Count;

            if (!comp.RecordState.TryGetValue(source, out var last) || !last.Track.SamePlayback(track))
            {
                tape.Events.Add(new BroadcastTapeEvent
                {
                    Time = position,
                    Kind = BroadcastTapeEventKind.TrackStart,
                    Channel = channel,
                    Sound = track.Sound.Path.ToString(),
                    Offset = now - track.StartTime,
                });
                tape.Events.Add(new BroadcastTapeEvent
                {
                    Time = position,
                    Kind = BroadcastTapeEventKind.Gain,
                    Channel = channel,
                    Gain = gain,
                });
            }
            else if (!MathHelper.CloseTo(last.Gain, gain))
            {
                RecordGain(tape, channel, gain, position);
            }

            comp.RecordState[source] = (track, gain);
        }

        foreach (var source in comp.RecordState.Keys.ToArray())
        {
            if (server.Comp.Tracks.ContainsKey(source))
                continue;

            StopRecordedChannel(comp, tape, source, position);
        }

        tape.Length = position;
    }

    private static void RecordGain(BroadcastTapeComponent tape, int channel, float gain, TimeSpan position)
    {
        for (var i = tape.Events.Count - 1; i >= 0; i--)
        {
            var ev = tape.Events[i];
            if (position - ev.Time > GainMergeWindow)
                break;

            if (ev.Channel != channel)
                continue;

            if (ev.Kind != BroadcastTapeEventKind.Gain)
                break;

            ev.Gain = gain;
            return;
        }

        tape.Events.Add(new BroadcastTapeEvent
        {
            Time = position,
            Kind = BroadcastTapeEventKind.Gain,
            Channel = channel,
            Gain = gain,
        });
    }

    private void StopRecordedChannel(BroadcastRecorderComponent comp, BroadcastTapeComponent tape, EntityUid source, TimeSpan position)
    {
        comp.RecordState.Remove(source);
        tape.Events.Add(new BroadcastTapeEvent
        {
            Time = position,
            Kind = BroadcastTapeEventKind.TrackStop,
            Channel = comp.RecordChannels[source],
        });
    }

    private void OnRadioSpoke(RadioSpokeEvent args)
    {
        var query = EntityQueryEnumerator<BroadcastRecorderComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Mode != BroadcastRecorderMode.Recording
                || args.Channel.ID != comp.Channel
                || args.Source == uid
                || GetTape((uid, comp)) is not { } tape)
                continue;

            var nameEv = new TransformSpeakerNameEvent(args.Source, Name(args.Source));
            RaiseLocalEvent(args.Source, nameEv);

            tape.Events.Add(new BroadcastTapeEvent
            {
                Time = _timing.CurTime - comp.Start,
                Kind = BroadcastTapeEventKind.Speech,
                Name = nameEv.VoiceName,
                Voice = CompOrNull<TextToSpeechComponent>(args.Source)?.VoicePrototypeId,
                Text = args.Message.Text,
                Language = args.Language.ID,
            });
        }
    }

    #endregion

    #region Playback

    public void StartPlayback(Entity<BroadcastRecorderComponent> ent, EntityUid? user = null)
    {
        if (ent.Comp.Mode != BroadcastRecorderMode.Stopped)
            Stop(ent);

        if (!CanRun(ent, user, out var tape, out var server))
            return;

        if (ent.Comp.Position >= tape.Length)
            ent.Comp.Position = TimeSpan.Zero;

        var comp = ent.Comp;
        comp.Start = _timing.CurTime - comp.Position;
        comp.Mode = BroadcastRecorderMode.Playing;

        var active = new Dictionary<int, BroadcastTapeEvent>();
        var gains = new Dictionary<int, float>();
        comp.NextEvent = tape.Events.Count;
        for (var i = 0; i < tape.Events.Count; i++)
        {
            var ev = tape.Events[i];
            if (ev.Time >= comp.Position)
            {
                comp.NextEvent = i;
                break;
            }

            switch (ev.Kind)
            {
                case BroadcastTapeEventKind.TrackStart:
                    active[ev.Channel] = ev;
                    break;
                case BroadcastTapeEventKind.TrackStop:
                    active.Remove(ev.Channel);
                    break;
                case BroadcastTapeEventKind.Gain:
                    gains[ev.Channel] = ev.Gain;
                    break;
            }
        }

        foreach (var (channel, gain) in gains)
            server.Comp.SourceGains[GetPlaybackChannel(ent, channel)] = gain;

        foreach (var (channel, ev) in active)
            PlayChannel(ent, server, channel, ev);

        _stationRadio.SyncReceivers(server);
        UpdateVisuals(ent);
        UpdateUi(ent);
    }

    private void UpdatePlayback(
        Entity<BroadcastRecorderComponent> ent,
        BroadcastTapeComponent tape,
        Entity<StationRadioServerComponent> server)
    {
        var comp = ent.Comp;
        var position = _timing.CurTime - comp.Start;
        var gainsChanged = false;

        while (comp.NextEvent < tape.Events.Count && tape.Events[comp.NextEvent].Time <= position)
        {
            var ev = tape.Events[comp.NextEvent++];
            switch (ev.Kind)
            {
                case BroadcastTapeEventKind.TrackStart:
                    PlayChannel(ent, server, ev.Channel, ev);
                    break;
                case BroadcastTapeEventKind.TrackStop:
                    if (comp.PlaybackChannels.TryGetValue(ev.Channel, out var stopped))
                        _stationRadio.StopTrack(stopped);
                    break;
                case BroadcastTapeEventKind.Gain:
                    server.Comp.SourceGains[GetPlaybackChannel(ent, ev.Channel)] = ev.Gain;
                    gainsChanged = true;
                    break;
                case BroadcastTapeEventKind.Speech:
                    Speak(ent, ev);
                    break;
            }
        }

        if (gainsChanged)
            _stationRadio.SyncReceivers(server);

        if (position < tape.Length)
            return;

        comp.Position = tape.Length;
        Stop(ent);
    }

    private void PlayChannel(
        Entity<BroadcastRecorderComponent> ent,
        Entity<StationRadioServerComponent> server,
        int channel,
        BroadcastTapeEvent ev)
    {
        if (ev.Sound == null)
            return;

        var start = ent.Comp.Start + ev.Time - ev.Offset;
        _stationRadio.PlayTrack(server, GetPlaybackChannel(ent, channel), new SoundPathSpecifier(ev.Sound), start);
    }

    private EntityUid GetPlaybackChannel(Entity<BroadcastRecorderComponent> ent, int channel)
    {
        if (!ent.Comp.PlaybackChannels.TryGetValue(channel, out var uid) || !Exists(uid))
        {
            ent.Comp.PlaybackChannels[channel] = uid = Spawn(ent.Comp.ChannelPrototype, MapCoordinates.Nullspace);
            EnsureComp<BroadcastRecorderChannelComponent>(uid).Recorder = ent;
        }

        return uid;
    }

    private void Speak(Entity<BroadcastRecorderComponent> ent, BroadcastTapeEvent ev)
    {
        if (string.IsNullOrEmpty(ev.Text))
            return;

        var voice = EnsureComp<VoiceOverrideComponent>(ent);
        voice.NameOverride = ev.Name;
        voice.Enabled = true;

        var tts = EnsureComp<TextToSpeechComponent>(ent);
        tts.VoicePrototypeId = ev.Voice;
        Dirty(ent, tts);

        LanguagePrototype? language = null;
        if (ev.Language != null)
            _proto.TryIndex(ev.Language, out language);

        _radio.SendRadioMessage(ent, ev.Text, ent.Comp.Channel, ent, language);
    }

    #endregion

    /// <summary>
    /// Stops recording or playback where it is.
    /// </summary>
    public void Stop(Entity<BroadcastRecorderComponent> ent, EntityUid? removedTape = null)
    {
        var comp = ent.Comp;
        if (comp.Mode == BroadcastRecorderMode.Stopped)
            return;

        var position = _timing.CurTime - comp.Start;
        var tape = CompOrNull<BroadcastTapeComponent>(removedTape) ?? GetTape(ent);

        if (comp.Mode == BroadcastRecorderMode.Recording && tape != null)
        {
            if (position > tape.MaxLength)
                position = tape.MaxLength;

            foreach (var source in comp.RecordState.Keys.ToArray())
                StopRecordedChannel(comp, tape, source, position);

            tape.Length = position;
        }

        _stationRadio.TryGetLinkedServer(ent, out var server);
        var serverComp = CompOrNull<StationRadioServerComponent>(server);
        foreach (var channel in comp.PlaybackChannels.Values)
        {
            _stationRadio.StopTrack(channel);
            serverComp?.SourceGains.Remove(channel);
            QueueDel(channel);
        }

        comp.PlaybackChannels.Clear();
        comp.RecordChannels.Clear();
        comp.RecordState.Clear();

        if (tape != null && position > tape.Length)
            position = tape.Length;

        comp.Position = position;
        comp.Mode = BroadcastRecorderMode.Stopped;
        UpdateVisuals(ent);
        Changed(ent);
    }

    private bool CanRun(
        Entity<BroadcastRecorderComponent> ent,
        EntityUid? user,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BroadcastTapeComponent? tape,
        out Entity<StationRadioServerComponent> server)
    {
        server = default;
        tape = GetTape(ent);
        string? error = null;

        if (tape == null)
            error = "broadcast-recorder-popup-no-tape";
        else if (!_power.IsPowered(ent.Owner))
            error = "broadcast-recorder-popup-no-power";
        else if (!_stationRadio.TryGetLinkedPoweredServer(ent, out var serverUid)
                 || !TryComp<StationRadioServerComponent>(serverUid, out var serverComp))
            error = "broadcast-recorder-popup-no-server";
        else
            server = (serverUid, serverComp);

        if (error == null && tape != null)
            return true;

        if (user != null && error != null)
            _popup.PopupEntity(Loc.GetString(error), ent, user.Value, PopupType.Medium);
        return false;
    }

    /// <summary>
    /// Plays the tape from where it is, or stops it if it is playing or recording.
    /// </summary>
    public void TogglePlayback(Entity<BroadcastRecorderComponent> ent, EntityUid? user = null)
    {
        if (ent.Comp.Mode == BroadcastRecorderMode.Stopped)
            StartPlayback(ent, user);
        else
            Stop(ent);
    }

    /// <summary>
    /// Back to the start of the tape, still playing if it was.
    /// </summary>
    public void Rewind(Entity<BroadcastRecorderComponent> ent, EntityUid? user = null)
    {
        var wasPlaying = ent.Comp.Mode == BroadcastRecorderMode.Playing;
        Stop(ent);
        ent.Comp.Position = TimeSpan.Zero;

        if (wasPlaying)
            StartPlayback(ent, user);
        else
            Changed(ent);
    }

    /// <summary>
    /// Name of the inserted tape, null without one.
    /// </summary>
    public string? GetTapeName(Entity<BroadcastRecorderComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.Slot, out var container) || container.ContainedEntities.Count == 0)
            return null;

        var tape = container.ContainedEntities[0];
        return HasComp<BroadcastTapeComponent>(tape) ? Name(tape) : null;
    }

    private void Changed(Entity<BroadcastRecorderComponent> ent)
    {
        UpdateUi(ent);
        if (_stationRadio.TryGetLinkedServer(ent, out var server) && TryComp<StationRadioServerComponent>(server, out var serverComp))
            _stationRadio.SyncReceivers((server, serverComp));
    }

    public BroadcastTapeComponent? GetTape(Entity<BroadcastRecorderComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.Slot, out var container) || container.ContainedEntities.Count == 0)
            return null;

        return CompOrNull<BroadcastTapeComponent>(container.ContainedEntities[0]);
    }

    private void UpdateVisuals(Entity<BroadcastRecorderComponent> ent)
    {
        _appearance.SetData(ent, BroadcastRecorderVisuals.Mode, ent.Comp.Mode);
        _appearance.SetData(ent, BroadcastRecorderVisuals.Tape, GetTape(ent) != null);
    }

    private void UpdateUi(Entity<BroadcastRecorderComponent> ent)
    {
        var tape = GetTape(ent);
        string? tapeName = null;
        if (_container.TryGetContainer(ent, ent.Comp.Slot, out var container) && container.ContainedEntities.Count > 0)
            tapeName = Name(container.ContainedEntities[0]);

        _ui.SetUiState(ent.Owner, BroadcastRecorderUiKey.Key, new BroadcastRecorderState(
            ent.Comp.Mode,
            tape != null ? tapeName : null,
            ent.Comp.Position,
            ent.Comp.Start,
            tape?.Length ?? TimeSpan.Zero,
            tape?.MaxLength ?? TimeSpan.Zero,
            _stationRadio.TryGetLinkedPoweredServer(ent, out _)));
    }

    private static string FormatTime(TimeSpan time) => $"{(int) time.TotalMinutes}:{time.Seconds:00}";
}
