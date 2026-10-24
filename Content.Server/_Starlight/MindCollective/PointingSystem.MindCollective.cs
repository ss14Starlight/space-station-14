using System.Linq;
using Content.Server._Starlight.MindCollective;
using Content.Shared._Starlight.MindCollective;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.Pointing.EntitySystems;

internal sealed partial class PointingSystem
{
    [Dependency] private MindCollectiveSystem _collectives = default!;

    private IEnumerable<ICommonSession>? GetCollectivePointingViewers(EntityUid pointer)
    {
        if (!HasComp<MindCollectiveObserverComponent>(pointer))
            return null;
        if (!_collectives.TryGetCollective(pointer, out _, out var collective)
            || !TryComp<ActorComponent>(pointer, out var actor) || !IsCollectiveViewer(actor.PlayerSession, collective)
            || actor.PlayerSession.AttachedEntity != pointer)
            return Array.Empty<ICommonSession>();
        return _playerManager.Sessions.Where(session => IsCollectiveViewer(session, collective)).ToArray();
    }

    private bool IsCollectiveViewer(ICommonSession session, MindCollectiveComponent collective)
        => session.AttachedEntity is { } entity && _collectives.IsAccepted(entity, collective)
           && _minds.TryGetMind(entity, out _, out var mind) && mind.UserId == session.UserId;

    private void SendCollectivePointingArrow(EntityUid pointer, MapCoordinates target, IEnumerable<ICommonSession> viewers)
    {
        RaiseNetworkEvent(new MindCollectivePointingEvent(target, _transform.GetMapCoordinates(pointer)),
            Filter.Empty().AddPlayers(viewers), recordReplay: false);
    }
}
