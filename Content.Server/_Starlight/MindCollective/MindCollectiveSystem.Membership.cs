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
    private uint _nextExpulsionRequest;

    /// <summary>Moves an unassociated mind to a pending observer and requests its player's consent.</summary>
    public bool RequestJoin(EntityUid collectiveId, EntityUid mind)
    {
        if (TerminatingOrDeleted(collectiveId) || TerminatingOrDeleted(mind)
            || !TryComp<MindCollectiveComponent>(collectiveId, out var collective) || collective.Closing
            || !TryComp<MindComponent>(mind, out var consciousness) || mind == collective.OwnerMind
            || collective.Minds.ContainsKey(mind) || HasComp<MindCollectiveMemberComponent>(mind)
            || HasComp<MindCollectiveComponent>(mind))
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
            RemoveMember(collectiveId, mind, createVacancy: false, preserveBody: true);
            return false;
        }

        if (consciousness.UserId is { } user && !collective.ExcludedUsers.Contains(user)
            && _players.TryGetSessionById(user, out var session) && session.Status == SessionStatus.InGame
            && session.AttachedEntity == observer)
        {
            var name = Comp<MindComponent>(collective.OwnerMind).CharacterName ?? Name(collective.OwnerMind);
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
            || !collective.PendingMinds.Remove(mind))
            return;
        CloseJoinConsent(mind);
        if (!collective.Minds.TryGetValue(mind, out var observer) || TerminatingOrDeleted(observer)
            || !TryComp<MindComponent>(mind, out var consciousness))
        {
            RemoveMember(collectiveId, mind, createVacancy: false);
            return;
        }
        if (!accepted || consciousness.OwnedEntity != observer || consciousness.UserId is not { } user
            || collective.ExcludedUsers.Contains(user) || !_players.TryGetSessionById(user, out var session)
            || session.Status != SessionStatus.InGame || session.AttachedEntity != observer)
        {
            RemoveMember(collectiveId, mind, createVacancy: true);
            return;
        }

        _joinPreviousBodies.Remove(mind, out var previous);
        FinishJoining(collectiveId, collective, mind, observer, previous);
    }

    private void FinishJoining(EntityUid collectiveId, MindCollectiveComponent collective, EntityUid mind,
        EntityUid observer, EntityUid? previous)
    {
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
            && (!TryGetActorMind(request.Actor, collectiveId, collective, out var owner)
                || owner != collective.OwnerMind || !collective.Minds.ContainsKey(request.Mind)))
            CancelExpulsion(collectiveId);
    }

    /// <summary>Revokes membership and control. Disbanding may preserve a non-observer body already occupied by the mind.</summary>
    public bool RemoveMember(EntityUid collectiveId, EntityUid mind, bool createVacancy = false, bool preserveBody = false)
    {
        if (!TryComp<MindCollectiveComponent>(collectiveId, out var collective)
            || mind == collective.OwnerMind && !collective.Closing || !collective.Minds.Remove(mind, out var observer))
            return false;

        collective.PendingMinds.Remove(mind);
        _joinPreviousBodies.Remove(mind);
        CloseJoinConsent(mind);
        if (_expulsionRequests.TryGetValue(collectiveId, out var challenge) && challenge.Mind == mind)
            CancelExpulsion(collectiveId);
        if (!TerminatingOrDeleted(observer))
        {
            RemoveObserverViews(observer);
            _ui.CloseUi(observer, MindCollectiveUiKey.Key);
        }

        EntityUid? previous = null;
        if (!TerminatingOrDeleted(mind) && TryComp<MindComponent>(mind, out var consciousness))
        {
            previous = consciousness.OwnedEntity;
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

            var keepBody = preserveBody && previous is { } current && !TerminatingOrDeleted(current)
                && !HasComp<MindCollectiveObserverComponent>(current);
            if (!keepBody)
            {
                if (consciousness.VisitingEntity is { } visiting && TryComp<GhostComponent>(visiting, out var ghost))
                    _ghosts.SetCanReturnToBody((visiting, ghost), false);
                var position = !TerminatingOrDeleted(observer) ? Transform(observer).Coordinates : (Robust.Shared.Map.EntityCoordinates?) null;
                _ghosts.SpawnGhost((mind, consciousness), position, canReturn: false);
                if (previous is { } vacated && collective.Bodies.Contains(vacated) && !TerminatingOrDeleted(vacated))
                    RaiseLocalEvent(vacated, new MindCollectiveBodyControlChangedEvent(mind, null));
            }
        }

        RaiseLocalEvent(collectiveId, new MindCollectiveLeftEvent(mind,
            previous is { } avatar && !HasComp<MindCollectiveObserverComponent>(avatar) ? previous : null));
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
               || !HasComp<MindCollectiveMemberComponent>(current) && !HasComp<MindCollectiveComponent>(current);
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
        if (!TryGetExpulsionOwner(actor, targetMind, out var collectiveId, out var collective))
            return false;
        _nextExpulsionRequest = unchecked(_nextExpulsionRequest + 1);
        if (_nextExpulsionRequest == 0)
            _nextExpulsionRequest++;
        _expulsionRequests[collectiveId] = new(actor, targetMind, _nextExpulsionRequest);
        UpdateUi(collectiveId, collective);
        return true;
    }

    public bool TryGetExpulsion(EntityUid collectiveId, out EntityUid targetMind, out uint request)
    {
        targetMind = default;
        request = 0;
        if (!_expulsionRequests.TryGetValue(collectiveId, out var challenge))
            return false;
        if (!TryGetExpulsionOwner(challenge.Actor, challenge.Mind, out var currentId, out _) || currentId != collectiveId)
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
            || challenge.Actor != actor || challenge.Mind != targetMind)
            return false;
        CancelExpulsion(collectiveId);
        var expelled = confirmed && Expel(actor, targetMind);
        UpdateUi(collectiveId, collective);
        return expelled;
    }

    public void CancelExpulsion(EntityUid collectiveId)
        => _expulsionRequests.Remove(collectiveId);

    private bool Expel(EntityUid actor, EntityUid targetMind)
    {
        if (!TryGetExpulsionOwner(actor, targetMind, out var collectiveId, out var collective))
            return false;
        if (TryComp<MindComponent>(targetMind, out var consciousness) && consciousness.UserId is { } user)
            collective.ExcludedUsers.Add(user);
        return RemoveMember(collectiveId, targetMind);
    }

    private bool TryGetExpulsionOwner(EntityUid actor, EntityUid targetMind, out EntityUid collectiveId,
        out MindCollectiveComponent collective)
    {
        return TryGetCollective(actor, out collectiveId, out collective) && !collective.Closing
               && TryGetActorMind(actor, collectiveId, collective, out var mind) && mind == collective.OwnerMind
               && targetMind != collective.OwnerMind && collective.Minds.ContainsKey(targetMind);
    }

    private readonly record struct ExpulsionRequest(EntityUid Actor, EntityUid Mind, uint Request);
}
