using System.Linq;
using Content.Server._Starlight.Changeling;
using Content.Shared._Starlight.Changeling;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.Pointing.EntitySystems;

internal sealed partial class PointingSystem
{
    private IEnumerable<ICommonSession>? GetMindspacePointingViewers(EntityUid pointer)
    {
        if (!TryComp<ChangelingMindspaceComponent>(pointer, out var observer))
            return null;

        // A missing or stale hive must never fall back to public pointing.
        if (!TryComp<ChangelingColonyComponent>(observer.Colony, out var colony) ||
            !_minds.TryGetMind(pointer, out var pointerMind, out _) ||
            !colony.Minds.TryGetValue(pointerMind, out var anchor) || anchor != pointer)
            return Array.Empty<ICommonSession>();

        return _playerManager.Sessions.Where(session =>
            _minds.TryGetMind(session, out var mind, out _) && colony.Minds.ContainsKey(mind) &&
            session.AttachedEntity is { } body &&
            (colony.Members.Contains(body) || colony.Minds.Values.Contains(body))).ToArray();
    }

    private void SendMindspacePointingArrow(EntityUid pointer, MapCoordinates target, IEnumerable<ICommonSession> viewers)
    {
        RaiseNetworkEvent(new ChangelingPointingEvent(target, _transform.GetMapCoordinates(pointer)),
            Filter.Empty().AddPlayers(viewers), recordReplay: false);
    }
}
