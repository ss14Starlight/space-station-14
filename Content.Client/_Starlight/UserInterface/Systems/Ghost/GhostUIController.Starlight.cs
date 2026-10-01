// ReSharper disable CheckNamespace
using Content.Client._Starlight.Replay;
using Robust.Client.Replays.Playback;
using Robust.Client.UserInterface;

namespace Content.Client.UserInterface.Systems.Ghost;

// Replays have no server to answer warp requests.
public sealed partial class GhostUIController
{
    [UISystemDependency] private readonly ReplayObserverSystem? _replayObserver = default;
    [Dependency] private IReplayPlaybackManager _replayPlayback = default!;

    private bool IsReplay() => _replayPlayback.Replay != null;

    private void StarlightUpdateGui() => Gui?.SetReplayMode(IsReplay(), OnReplayRoundSummaryPressed);

    private void OnReplayRoundSummaryPressed() => _replayObserver?.OpenRoundSummaryOrNotify();

    private bool TryReplayRequestWarps()
    {
        if (!IsReplay())
            return false;

        if (Gui == null)
            return true;

        if (_replayObserver != null)
            Gui.TargetWindow.UpdateWarps(_replayObserver.GetReplayWarps());

        Gui.TargetWindow.Populate();
        Gui.TargetWindow.OpenCentered();
        return true;
    }

    private bool TryReplayWarp(NetEntity target)
    {
        if (!IsReplay())
            return false;

        _replayObserver?.WarpTo(target);
        return true;
    }

    private bool TryReplayGhostnado()
    {
        if (!IsReplay())
            return false;

        _replayObserver?.WarpToMostFollowed();
        return true;
    }
}
