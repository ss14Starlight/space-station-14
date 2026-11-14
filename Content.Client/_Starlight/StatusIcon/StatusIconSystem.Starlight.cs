// ReSharper disable CheckNamespace

using Content.Client._Starlight.Replay;
using Content.Shared.StatusIcon;

namespace Content.Client.StatusIcon;

public sealed partial class StatusIconSystem
{
    [Dependency] private ReplayObserverSystem _replayObserver = default!;

    // The replay observer sees faction icons only through its faction toggle.
    private bool? ReplayFactionIconVisible(EntityUid? viewer, StatusIconData data) =>
        data is FactionIconPrototype ? _replayObserver.FactionIconsVisibleTo(viewer) : null;
}
