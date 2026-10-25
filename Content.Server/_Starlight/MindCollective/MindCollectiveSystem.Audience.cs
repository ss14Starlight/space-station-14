using Content.Shared.Mind;
using Robust.Shared.Player;

namespace Content.Server._Starlight.MindCollective;

public enum MindCollectiveCommunicationKind : byte { Chat, Pointing }

/// <summary>Consumers may explicitly extend or restrict the default private audience.</summary>
public sealed class MindCollectiveAudienceEvent(EntityUid sender, MindCollectiveCommunicationKind kind, HashSet<ICommonSession> recipients) : EntityEventArgs
{
    public readonly EntityUid Sender = sender;
    public readonly MindCollectiveCommunicationKind Kind = kind;
    public readonly HashSet<ICommonSession> Recipients = recipients;
}

public sealed partial class MindCollectiveSystem
{
    public HashSet<ICommonSession> GetCommunicationAudience(EntityUid sender, MindCollectiveCommunicationKind kind)
    {
        var recipients = new HashSet<ICommonSession>();
        if (!TryGetActorCollective(sender, out var id, out var collective, out _))
            return recipients;
        foreach (var mind in collective.Minds.Keys)
        {
            if (TryComp<MindComponent>(mind, out var member) && member.UserId is { } user
                && _players.TryGetSessionById(user, out var session) && session.AttachedEntity is { } actor
                && TryGetActorMind(actor, id, collective, out _))
                recipients.Add(session);
        }
        var ev = new MindCollectiveAudienceEvent(sender, kind, recipients);
        RaiseLocalEvent(id, ev);
        return recipients;
    }
}
