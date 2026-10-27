using System.Linq;
using Content.Server._Starlight.MindCollective;
using Content.Shared._Starlight.MindCollective;
using Robust.Shared.Map;
using Robust.Shared.Player;

// ReSharper disable once CheckNamespace
namespace Content.Server.Pointing.EntitySystems;

internal sealed partial class PointingSystem
{
    [Dependency] private MindCollectiveSystem _collectives = default!;

    private IEnumerable<ICommonSession>? GetCollectivePointingViewers(EntityUid pointer)
    {
        if (!TryComp<MindCollectiveObserverComponent>(pointer, out var observer))
            return null;
        if (!_collectives.TryGetCollective(pointer, out _, out var collective)
            || !TryComp<ActorComponent>(pointer, out var actor) || !IsCollectiveViewer(actor.PlayerSession, collective)
            || actor.PlayerSession.AttachedEntity != pointer)
            return Array.Empty<ICommonSession>();
        if (observer.AllowWorldPointing)
            return null;
        return _collectives.GetCommunicationAudience(pointer, MindCollectiveCommunicationKind.Pointing);
    }

    private bool IsCollectiveViewer(ICommonSession session, MindCollectiveComponent collective)
        => session.AttachedEntity is { } entity && _collectives.IsAccepted(entity, collective)
           && _minds.TryGetMind(entity, out _, out var mind) && mind.UserId == session.UserId;

    private bool TrySendPrivatePointingArrow(EntityUid pointer, MapCoordinates target, out IEnumerable<ICommonSession>? viewers)
    {
        viewers = GetCollectivePointingViewers(pointer);
        if (viewers == null)
            return false;

        RaiseNetworkEvent(new MindCollectivePointingEvent(target, _transform.GetMapCoordinates(pointer)),
            Filter.Empty().AddPlayers(viewers), recordReplay: false);
        return true;
    }
}
