using Content.Client.RoundEnd;
using Content.Shared.GameTicking;
using Robust.Client.UserInterface;
using Robust.Shared.Console;
using Robust.Shared.Serialization.Markdown.Mapping;

namespace Content.Client._Starlight.Replay;

public sealed partial class ReplayObserverSystem
{
    [Dependency] private IConsoleHost _conHost = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!;

    private const string RoundSummaryCommand = "replay_round_summary";

    // The summary is sent a few minutes before the recording ends; this bounds how far back we load.
    private static readonly TimeSpan RoundEndSearchWindow = TimeSpan.FromMinutes(20);

    private bool _roundEndSearched;
    private RoundEndMessageEvent? _roundEnd;

    private void InitializeRoundSummary()
    {
        _replayPlayback.ReplayPlaybackStarted += OnRoundSummaryPlaybackStarted;
        _replayPlayback.ReplayPlaybackStopped += OnRoundSummaryPlaybackStopped;
    }

    private void ShutdownRoundSummary()
    {
        _replayPlayback.ReplayPlaybackStarted -= OnRoundSummaryPlaybackStarted;
        _replayPlayback.ReplayPlaybackStopped -= OnRoundSummaryPlaybackStopped;
    }

    private void OnRoundSummaryPlaybackStarted(MappingDataNode yamlMappingNode, List<object> objects)
    {
        _roundEndSearched = false;
        _roundEnd = null;

        _conHost.RegisterCommand(RoundSummaryCommand,
            Loc.GetString("cmd-replay-round-summary-desc"),
            Loc.GetString("cmd-replay-round-summary-help"),
            RoundSummaryCommandCallback);
    }

    private void OnRoundSummaryPlaybackStopped()
    {
        _conHost.UnregisterCommand(RoundSummaryCommand);
        _roundEndSearched = false;
        _roundEnd = null;
    }

    private void RoundSummaryCommandCallback(IConsoleShell shell, string argStr, string[] args)
    {
        if (!TryOpenRoundSummary())
            shell.WriteError(Loc.GetString("replay-observer-round-summary-missing"));
    }

    public void OpenRoundSummaryOrNotify()
    {
        if (TryOpenRoundSummary())
            return;

        if (_player.LocalEntity is { } ent)
            _popup.PopupEntity(Loc.GetString("replay-observer-round-summary-missing"), ent);
    }

    public bool TryOpenRoundSummary()
    {
        if (FindRoundEnd() is not { } roundEnd)
            return false;

        // The controller won't re-open a window for a round it has already shown.
        var controller = _uiManager.GetUIController<RoundEndSummaryUIController>();
        controller.DeleteRoundEndSummaryWindow();
        controller.OpenRoundEndSummaryWindow(roundEnd);
        return true;
    }

    private RoundEndMessageEvent? FindRoundEnd()
    {
        if (_roundEndSearched)
            return _roundEnd;

        _roundEndSearched = true;

        if (_replayPlayback.Replay is not { Count: > 0 } replay)
            return null;

        var last = replay.Count - 1;
        var cutoff = replay.ReplayTime[last] - RoundEndSearchWindow;

        for (var i = last; i >= 0 && replay.ReplayTime[i] >= cutoff; i--)
        {
            foreach (var message in replay.GetMessages(i).Messages)
            {
                if (message is RoundEndMessageEvent roundEnd)
                    return _roundEnd = roundEnd;
            }
        }

        return null;
    }
}
