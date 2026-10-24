using System.Linq;
using Content.Server.EUI;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Actions.Components;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.MindCollective;

public sealed partial class MindCollectiveSystem
{
    [Dependency] private EuiManager _membershipEui = default!;
    [Dependency] private GhostRoleSystem _membershipGhostRoles = default!;
    [Dependency] private IPrototypeManager _membershipPrototypes = default!;

    private readonly Dictionary<EntityUid, (EntityUid Collective, AcceptMindCollectiveEui Eui)> _joinConsents = new();
    private readonly Dictionary<EntityUid, EntityUid?> _joinPreviousBodies = new();
    private readonly Dictionary<EntityUid, ExpulsionRequest> _expulsionRequests = new();
    private readonly HashSet<EntityUid> _departingMinds = new();
    private uint _nextExpulsionRequest;

    /// <summary>Moves an unassociated mind to a pending observer and requests its player's consent.</summary>
    public bool RequestJoin(EntityUid collectiveId, EntityUid mind)
    {
        if (TerminatingOrDeleted(collectiveId) || TerminatingOrDeleted(mind)
            || !TryComp<MindCollectiveComponent>(collectiveId, out var collective) || collective.Closing
            || !TryComp<MindComponent>(mind, out var consciousness)
            || collective.Minds.ContainsKey(mind) || HasComp<MindCollectiveMemberComponent>(mind))
            return false;

        var previous = consciousness.OwnedEntity;
        if (previous is { } source && TryGetCollective(source, out var previousCollective, out _)
            && previousCollective != collectiveId)
            return false;
        var observer = CreateObserver(collectiveId, collective, mind, previous ?? collective.ActiveBody);
        collective.PendingMinds.Add(mind);
        _joinPreviousBodies[mind] = previous;
        _mind.TransferTo(mind, observer, ghostCheckOverride: true, createGhost: false, mind: consciousness);
        if (collective.Closing || consciousness.OwnedEntity != observer)
        {
            RemoveMember(collectiveId, mind, disposition: collective.DeclineDisposition);
            return false;
        }

        if (consciousness.UserId is { } user && !collective.ExcludedUsers.Contains(user)
            && _players.TryGetSessionById(user, out var session) && session.Status == SessionStatus.InGame
            && session.AttachedEntity == observer)
        {
            var name = Name(collectiveId);
            var eui = new AcceptMindCollectiveEui(collectiveId, mind, this, name,
                Loc.GetString(collective.JoinTitle), Loc.GetString(collective.JoinDescription, ("name", name)));
            _joinConsents.Add(mind, (collectiveId, eui));
            _membershipEui.OpenEui(eui, session);
        }
        // Offline or excluded minds are declined by the next update, after the caller finishes its operation.
        UpdateUi(collectiveId, collective);
        return true;
    }

    /// <summary>Resolves a pending player's choice once without changing their roles, objectives or other actions.</summary>
    public void RespondJoin(EntityUid collectiveId, EntityUid mind, bool accepted)
    {
        if (!TryComp<MindCollectiveComponent>(collectiveId, out var collective) || collective.Closing
            || !collective.PendingMinds.Contains(mind))
            return;
        CloseJoinConsent(mind);
        if (!collective.Minds.TryGetValue(mind, out var observer) || TerminatingOrDeleted(observer)
            || !TryComp<MindComponent>(mind, out var consciousness))
        {
            RemoveMember(collectiveId, mind, disposition: collective.DeclineDisposition);
            return;
        }
        if (!accepted || consciousness.OwnedEntity != observer || consciousness.UserId is not { } user
            || collective.ExcludedUsers.Contains(user) || !_players.TryGetSessionById(user, out var session)
            || session.Status != SessionStatus.InGame || session.AttachedEntity != observer)
        {
            RemoveMember(collectiveId, mind, createVacancy: collective.CreateVacancyOnDecline,
                disposition: collective.DeclineDisposition);
            return;
        }

        collective.PendingMinds.Remove(mind);
        _joinPreviousBodies.TryGetValue(mind, out var previous);
        FinishJoining(collectiveId, collective, mind, observer, previous);
    }

    private void FinishJoining(EntityUid collectiveId, MindCollectiveComponent collective, EntityUid mind,
        EntityUid observer, EntityUid? previous)
    {
        EnsureDefaultRole(collectiveId, collective, mind);
        EnsureControlAction(mind);
        Comp<MindCollectiveObserverComponent>(observer).OpenUiAt = _timing.CurTime + TimeSpan.FromMilliseconds(100);
        RaiseLocalEvent(collectiveId, new MindCollectiveJoinedEvent(mind, previous));
        UpdateUi(collectiveId, collective);
    }

    private void CloseJoinConsent(EntityUid mind)
    {
        if (_joinConsents.Remove(mind, out var consent))
            consent.Eui.Dismiss();
    }

    private void CloseConsents(EntityUid collectiveId)
    {
        foreach (var (mind, consent) in _joinConsents.ToArray())
        {
            if (consent.Collective == collectiveId)
                CloseJoinConsent(mind);
        }
        CancelExpulsion(collectiveId);
    }

    private void UpdateMembership(EntityUid collectiveId, MindCollectiveComponent collective)
    {
        foreach (var mind in collective.PendingMinds.ToArray())
        {
            if (!collective.Minds.TryGetValue(mind, out var observer) || TerminatingOrDeleted(observer)
                || !TryComp<MindComponent>(mind, out var consciousness) || consciousness.UserId is not { } user
                || collective.ExcludedUsers.Contains(user) || !_players.TryGetSessionById(user, out var session)
                || session.Status != SessionStatus.InGame || session.AttachedEntity != observer)
                RespondJoin(collectiveId, mind, false);
        }
        collective.VacantObservers.RemoveWhere(observer => TerminatingOrDeleted(observer));
        if (_expulsionRequests.TryGetValue(collectiveId, out var request)
            && (!TryGetExpulsionAuthority(request.Actor, request.Mind, out var current, out _, out var actorMind)
                || current != collectiveId || actorMind != request.ActorMind))
            CancelExpulsion(collectiveId);
    }

    /// <summary>Revokes membership and applies the configured outcome without changing unrelated roles or objectives.</summary>
    public bool RemoveMember(EntityUid collectiveId, EntityUid mind, bool createVacancy = false,
        MindCollectiveDepartureDisposition? disposition = null)
    {
        if (!_departingMinds.Add(mind))
            return false;
        try
        {
            return RemoveMemberInternal(collectiveId, mind, createVacancy, disposition);
        }
        finally
        {
            _departingMinds.Remove(mind);
        }
    }

    private bool RemoveMemberInternal(EntityUid collectiveId, EntityUid mind, bool createVacancy,
        MindCollectiveDepartureDisposition? disposition)
    {
        if (!TryComp<MindCollectiveComponent>(collectiveId, out var collective)
            || !collective.Minds.TryGetValue(mind, out var observer))
            return false;

        var currentBody = TryComp<MindComponent>(mind, out var departing) ? departing.OwnedEntity : null;
        _joinPreviousBodies.TryGetValue(mind, out var joinedFrom);
        var leaving = new MindCollectiveMemberLeavingEvent(mind, currentBody, joinedFrom,
            disposition ?? (collective.PendingMinds.Contains(mind) ? collective.DeclineDisposition : collective.ExpelDisposition));
        RaiseLocalEvent(collectiveId, leaving);
        if (!collective.Minds.TryGetValue(mind, out observer))
            return false;
        RemoveMemberRoles(collectiveId, collective, mind);
        collective.Minds.Remove(mind);
        collective.PendingMinds.Remove(mind);
        _joinPreviousBodies.Remove(mind);
        CloseJoinConsent(mind);
        if (_expulsionRequests.TryGetValue(collectiveId, out var challenge)
            && (challenge.Mind == mind || challenge.ActorMind == mind))
            CancelExpulsion(collectiveId);
        if (!TerminatingOrDeleted(observer))
        {
            RemoveObserverViews(observer);
            if (TryComp<MindCollectiveObserverComponent>(observer, out var view))
            {
                view.AllowWorldInteraction = false;
                view.AllowConsciousActions = false;
                view.AllowWorldSpeech = false;
                view.AllowWorldPointing = false;
                Dirty(observer, view);
            }
            _ui.CloseUi(observer, MindCollectiveUiKey.Key);
        }

        if (!TerminatingOrDeleted(mind) && TryComp<MindComponent>(mind, out var consciousness))
        {
            var previous = consciousness.OwnedEntity;
            if (previous is { } body && !TerminatingOrDeleted(body))
                _ui.CloseUi(body, MindCollectiveUiKey.Key);
            if (TryComp<ActionsContainerComponent>(mind, out var actions))
            {
                foreach (var action in actions.Container.ContainedEntities.ToArray())
                {
                    if (TerminatingOrDeleted(action) || MetaData(action).EntityPrototype?.ID != ControlAction)
                        continue;
                    _actions.RemoveAction(action);
                    QueueDel(action);
                }
            }
            if (TryComp<MindCollectiveMemberComponent>(mind, out var member) && member.Collective == collectiveId)
                RemComp<MindCollectiveMemberComponent>(mind);

            if (leaving.Disposition == MindCollectiveDepartureDisposition.ReturnToPreviousBody
                && joinedFrom is { } destination && !TerminatingOrDeleted(destination)
                && !HasComp<MindCollectiveObserverComponent>(destination)
                && (!_mind.TryGetMind(destination, out var occupant, out _) || occupant == mind)
                && (!TryComp<ActorComponent>(destination, out var player)
                    || player.PlayerSession.UserId == consciousness.UserId)
                && (!TryGetCollective(destination, out var previousCollective, out _) || previousCollective == collectiveId))
                _mind.TransferTo(mind, destination, ghostCheckOverride: true, createGhost: false, mind: consciousness);

            if (leaving.Disposition == MindCollectiveDepartureDisposition.Ghost)
            {
                if (consciousness.VisitingEntity is { } visiting && TryComp<GhostComponent>(visiting, out var ghost))
                    _ghosts.SetCanReturnToBody((visiting, ghost), false);
                var position = previous is { } source && !TerminatingOrDeleted(source)
                    ? Transform(source).Coordinates
                    : !TerminatingOrDeleted(observer) ? Transform(observer).Coordinates : (Robust.Shared.Map.EntityCoordinates?) null;
                _ghosts.SpawnGhost((mind, consciousness), position, canReturn: false);
            }
            else if (consciousness.OwnedEntity == observer)
                _mind.TransferTo(mind, null, ghostCheckOverride: true, createGhost: false, mind: consciousness);

            if (previous != consciousness.OwnedEntity)
            {
                if (previous is { } vacated && collective.Bodies.Contains(vacated) && !TerminatingOrDeleted(vacated))
                    RaiseLocalEvent(vacated, new MindCollectiveBodyControlChangedEvent(mind, null));
                if (consciousness.OwnedEntity is { } entered && collective.Bodies.Contains(entered) && !TerminatingOrDeleted(entered))
                    RaiseLocalEvent(entered, new MindCollectiveBodyControlChangedEvent(null, mind));
            }
        }

        RaiseLocalEvent(collectiveId, new MindCollectiveLeftEvent(mind,
            currentBody is { } avatar && avatar != observer && !HasComp<MindCollectiveObserverComponent>(avatar) ? currentBody : null));
        if (!TerminatingOrDeleted(observer))
        {
            if (createVacancy && !collective.Closing && collective.VacancyPrototype != null)
                PublishVacancy(observer, collective);
            else
                QueueDel(observer);
        }
        UpdateUi(collectiveId, collective);
        return true;
    }

    private void PublishVacancy(EntityUid observer, MindCollectiveComponent collective)
    {
        if (collective.VacancyPrototype is not { } prototype || TerminatingOrDeleted(observer))
            return;
        collective.VacantObservers.Add(observer);
        EntityManager.AddComponents(observer, _membershipPrototypes.Index(prototype), removeExisting: false);
    }

    private bool CanRequestVacancy(EntityUid observer, EntityUid collectiveId, MindCollectiveComponent collective,
        ICommonSession player)
    {
        if (collective.Closing || TerminatingOrDeleted(collectiveId) || TerminatingOrDeleted(observer)
            || !collective.VacantObservers.Contains(observer) || collective.ExcludedUsers.Contains(player.UserId)
            || _mind.TryGetMind(observer, out _, out _))
            return false;
        return !_mind.TryGetMind(player.UserId, out var current, out _)
               || !HasComp<MindCollectiveMemberComponent>(current);
    }

    [SubscribeLocalEvent]
    private void OnVacancyRequest(Entity<MindCollectiveObserverComponent> ent, ref GhostRoleRequestAttemptEvent args)
    {
        args.Cancelled |= !TryComp<MindCollectiveComponent>(ent.Comp.Collective, out var collective)
                          || !CanRequestVacancy(ent, ent.Comp.Collective, collective, args.Player);
    }

    [SubscribeLocalEvent]
    private void OnTakeVacancy(Entity<MindCollectiveObserverComponent> ent, ref TakeGhostRoleEvent args)
    {
        var collectiveId = ent.Comp.Collective;
        if (args.TookRole || !TryComp<MindCollectiveComponent>(collectiveId, out var collective)
            || !CanRequestVacancy(ent, collectiveId, collective, args.Player)
            || args.Player.Status != SessionStatus.InGame || !HasComp<GhostComponent>(args.Player.AttachedEntity)
            || !TryComp<GhostRoleComponent>(ent, out var role) || role.Taken)
            return;

        collective.VacantObservers.Remove(ent);
        _membershipGhostRoles.MarkGhostRoleTaken((ent, role));
        _membershipGhostRoles.GhostRoleInternalCreateMindAndTransfer(args.Player, ent, ent, role);
        if (collective.Closing || TerminatingOrDeleted(ent))
            return;
        if (!_mind.TryGetMind(ent, out var mind, out var consciousness) || consciousness.UserId != args.Player.UserId)
        {
            RemComp<GhostRoleComponent>(ent);
            PublishVacancy(ent, collective);
            return;
        }
        collective.Minds.Add(mind, ent);
        EnsureComp<MindCollectiveMemberComponent>(mind).Collective = collectiveId;
        RemCompDeferred<GhostRoleComponent>(ent);
        FinishJoining(collectiveId, collective, mind, ent, null);
        args.TookRole = true;
    }

    public bool RequestExpulsion(EntityUid actor, EntityUid targetMind)
    {
        if (!TryGetExpulsionAuthority(actor, targetMind, out var collectiveId, out var collective, out var actorMind))
            return false;
        _nextExpulsionRequest = unchecked(_nextExpulsionRequest + 1);
        if (_nextExpulsionRequest == 0)
            _nextExpulsionRequest++;
        _expulsionRequests[collectiveId] = new(actor, actorMind, targetMind, _nextExpulsionRequest);
        UpdateUi(collectiveId, collective);
        return true;
    }

    public bool TryGetExpulsion(EntityUid collectiveId, EntityUid actorMind, out EntityUid targetMind, out uint request)
    {
        targetMind = default;
        request = 0;
        if (!_expulsionRequests.TryGetValue(collectiveId, out var challenge) || challenge.ActorMind != actorMind)
            return false;
        if (!TryGetExpulsionAuthority(challenge.Actor, challenge.Mind, out var currentId, out _, out var currentMind)
            || currentId != collectiveId || currentMind != actorMind)
        {
            CancelExpulsion(collectiveId);
            return false;
        }
        targetMind = challenge.Mind;
        request = challenge.Request;
        return true;
    }

    public bool RespondExpulsion(EntityUid actor, EntityUid targetMind, uint request, bool confirmed)
    {
        if (!TryGetCollective(actor, out var collectiveId, out var collective)
            || !_expulsionRequests.TryGetValue(collectiveId, out var challenge) || challenge.Request != request
            || challenge.Actor != actor || challenge.Mind != targetMind
            || !TryGetActorMind(actor, collectiveId, collective, out var actorMind) || actorMind != challenge.ActorMind)
            return false;
        CancelExpulsion(collectiveId);
        var expelled = confirmed && Expel(actor, targetMind);
        UpdateUi(collectiveId, collective);
        return expelled;
    }

    public void CancelExpulsion(EntityUid collectiveId, EntityUid? actorMind = null)
    {
        if (actorMind == null || _expulsionRequests.TryGetValue(collectiveId, out var challenge) && challenge.ActorMind == actorMind)
            _expulsionRequests.Remove(collectiveId);
    }

    private bool Expel(EntityUid actor, EntityUid targetMind)
    {
        if (!TryGetExpulsionAuthority(actor, targetMind, out var collectiveId, out var collective, out _))
            return false;
        if (collective.ExcludeExpelledUsers && TryComp<MindComponent>(targetMind, out var consciousness)
            && consciousness.UserId is { } user)
            collective.ExcludedUsers.Add(user);
        return RemoveMember(collectiveId, targetMind, createVacancy: collective.CreateVacancyOnExpel,
            disposition: collective.ExpelDisposition);
    }

    private bool TryGetExpulsionAuthority(EntityUid actor, EntityUid targetMind, out EntityUid collectiveId,
        out MindCollectiveComponent collective, out EntityUid actorMind)
    {
        actorMind = default;
        return TryGetCollective(actor, out collectiveId, out collective) && !collective.Closing
               && TryGetActorMind(actor, collectiveId, collective, out actorMind)
               && CanManageMember(actorMind, targetMind, "mind-collective.expel");
    }

    private readonly record struct ExpulsionRequest(EntityUid Actor, EntityUid ActorMind, EntityUid Mind, uint Request);
}

/// <summary>Raised before removing membership so consumers can choose the departure outcome or transfer the mind.</summary>
public sealed class MindCollectiveMemberLeavingEvent(EntityUid mind, EntityUid? currentBody, EntityUid? previousBody,
    MindCollectiveDepartureDisposition disposition) : EntityEventArgs
{
    public readonly EntityUid Mind = mind;
    public readonly EntityUid? CurrentBody = currentBody;
    public readonly EntityUid? PreviousBody = previousBody;
    public MindCollectiveDepartureDisposition Disposition = disposition;
}
