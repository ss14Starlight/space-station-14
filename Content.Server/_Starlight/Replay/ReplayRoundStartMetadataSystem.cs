using Content.Server.GameTicking;
using Content.Shared._Starlight.Replay;
using Robust.Shared.Replays;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server._Starlight.Replay;

/// <summary>
/// Writes the round start time into replay metadata, so viewers can show and jump to round time.
/// </summary>
public sealed partial class ReplayRoundStartMetadataSystem : EntitySystem
{
    [Dependency] private IReplayRecordingManager _replay = default!;
    [Dependency] private GameTicker _ticker = default!;

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

    private void OnRecordingStarted(MappingDataNode metadata, List<object> events) =>
        metadata[ReplayRoundTime.MetaKeyRoundStart] = new ValueDataNode(_ticker.RoundStartTimeSpan.Ticks.ToString());
}
