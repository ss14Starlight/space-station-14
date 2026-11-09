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
public abstract class SharedReplayRoundStartMetadataSystem : EntitySystem
{
    [Dependency] private IReplayRecordingManager _replay = default!;
    [Dependency] private SharedGameTicker _ticker = default!;

    // Whether a round is underway, so the ticker's round start is this round's and not the last one's.
    protected abstract bool RoundStarted { get; }

    public override void Initialize()
    {
        base.Initialize();

        _replay.RecordingStarted += OnRecordingStarted;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _replay.RecordingStarted -= OnRecordingStarted;
    }

    // A recording started in the lobby gets no round start, since the metadata is written before the round starts;
    // viewers then count from the recording start.
    private void OnRecordingStarted(MappingDataNode metadata, List<object> events)
    {
        if (RoundStarted)
            metadata[ReplayRoundTime.MetaKeyRoundStart] = new ValueDataNode(_ticker.RoundStartTimeSpan.Ticks.ToString(CultureInfo.InvariantCulture));
    }
}
