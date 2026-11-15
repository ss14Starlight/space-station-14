using System.Globalization;
using Content.Shared.GameTicking;
using Robust.Shared.Replays;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Shared._Starlight.Replay;

/// <summary>
/// Writes the round start time into replay metadata, so viewers can show and jump to round time.
/// Covers server recordings and client-side recordings.
/// </summary>
public abstract partial class SharedReplayRoundStartMetadataSystem : EntitySystem
{
    [Dependency] private IReplayRecordingManager _replay = default!;
    [Dependency] private SharedGameTicker _ticker = default!;

    // Whether a round is underway, so the ticker's round start is this round's and not the last one's.
    protected abstract bool RoundStarted { get; }

    // The first round start seen during the current recording, kept until it stops.
    private TimeSpan? _recordingRoundStart;

    public override void Initialize()
    {
        base.Initialize();

        _replay.RecordingStarted += OnRecordingStarted;
        _replay.RecordingStopped += OnRecordingStopped;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _replay.RecordingStarted -= OnRecordingStarted;
        _replay.RecordingStopped -= OnRecordingStopped;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // A recording started in the lobby picks up the round start once the round begins.
        if (_recordingRoundStart == null && _replay.IsRecording && RoundStarted)
            _recordingRoundStart = _ticker.RoundStartTimeSpan;
    }

    // A recording started in the lobby gets no round start here, since this metadata is written before the round starts.
    private void OnRecordingStarted(MappingDataNode metadata, List<object> events)
    {
        _recordingRoundStart = RoundStarted ? _ticker.RoundStartTimeSpan : null;
        WriteRoundStart(metadata);
    }

    // The final metadata gets the first round start seen while recording, even if a later round or the lobby
    // follows before recording stops.
    private void OnRecordingStopped(MappingDataNode metadata)
    {
        WriteRoundStart(metadata);
        _recordingRoundStart = null;
    }

    private void WriteRoundStart(MappingDataNode metadata)
    {
        if (_recordingRoundStart is { } roundStart)
            metadata[ReplayRoundTime.MetaKeyRoundStart] = new ValueDataNode(roundStart.Ticks.ToString(CultureInfo.InvariantCulture));
    }
}
