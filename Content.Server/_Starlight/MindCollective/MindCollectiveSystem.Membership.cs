using System.Linq;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._Starlight.MindCollective;

public sealed partial class MindCollectiveSystem
{
    [Dependency] private GhostRoleSystem _membershipGhostRoles = default!;

    /// <summary>Moves an unassociated mind to a pending observer and requests its player's consent.</summary>
    public bool RequestJoin(EntityUid collectiveId, EntityUid mind)
    {
        if (TerminatingOrDeleted(collectiveId) || TerminatingOrDeleted(mind)
            || !TryComp<MindCollectiveComponent>(collectiveId, out var collective) || collective.Closing
            || !TryComp<MindComponent>(mind, out var consciousness)
            || collective.Members.ContainsKey(mind) || HasComp<MindCollectiveMemberComponent>(mind))
            return false;

        var previous = consciousness.OwnedEntity;
        if (previous is { } source && TryGetCollective(source, out var previousCollective, out _)
            && previousCollective != collectiveId)
            return false;
        var member = AddMember(collectiveId, collective, mind, previous ?? collective.ActiveBody, pending: true);
        member.PreviousBody = previous;
        if (previous is { } ghost && HasComp<GhostComponent>(ghost))
            member.PreviousGhostPosition = Transform(ghost).Coordinates;
        _mind.TransferTo(mind, member.Observer, ghostCheckOverride: true, createGhost: false, mind: consciousness);
        if (collective.Closing || consciousness.OwnedEntity != member.Observer)
        {
            RemoveMember(collectiveId, mind, disposition: collective.DeclineDisposition);
            return false;
        }
        if (consciousness.UserId is { } user && !collective.ExcludedUsers.Contains(user)
            && _players.TryGetSessionById(user, out var session) && session.Status == SessionStatus.InGame
            && session.AttachedEntity == member.Observer)
        {
            var name = Name(collectiveId);
            member.Consent = new AcceptMindCollectiveEui(collectiveId, mind, this, name,
                Loc.GetString(collective.JoinTitle), Loc.GetString(collective.JoinDescription, ("name", name)));
            _eui.OpenEui(member.Consent, session);
        }
        // Offline or excluded minds are declined after the caller finishes its operation.
        UpdateUi(collectiveId, collective);
        return true;
    }

    /// <summary>Resolves a pending player's choice once without changing unrelated roles or objectives.</summary>
    public void RespondJoin(EntityUid collectiveId, EntityUid mind, bool accepted)
    {
        if (!TryComp<MindCollectiveComponent>(collectiveId, out var collective) || collective.Closing
            || !collective.Members.TryGetValue(mind, out var member) || !member.Pending || member.Leaving)
            return;
        CloseConsent(member);
        if (!accepted || TerminatingOrDeleted(member.Observer) || !TryComp<MindComponent>(mind, out var consciousness)
            || consciousness.OwnedEntity != member.Observer || consciousness.UserId is not { } user
            || collective.ExcludedUsers.Contains(user) || !_players.TryGetSessionById(user, out var session)
            || session.Status != SessionStatus.InGame || session.AttachedEntity != member.Observer)
        {
            RemoveMember(collectiveId, mind, createVacancy: collective.CreateVacancyOnDecline,
                disposition: collective.DeclineDisposition);
            return;
        }
        member.Pending = false;
        FinishJoining(collectiveId, collective, mind, member.PreviousBody);
    }

    private void FinishJoining(EntityUid collectiveId, MindCollectiveComponent collective, EntityUid mind, EntityUid? previous)
    {
        EnsureDefaultRole(collectiveId, collective, mind);
        EnsureControlAction(mind);
        RaiseLocalEvent(collectiveId, new MindCollectiveJoinedEvent(mind, previous));
        OpenMenu(collectiveId, collective, mind);
        UpdateUi(collectiveId, collective);
    }

    private static void CloseConsent(MindCollectiveMemberState member)
    {
        var consent = member.Consent;
        member.Consent = null;
        consent?.Dismiss();
    }

    private void UpdateMembership(EntityUid collectiveId, MindCollectiveComponent collective)
    {
        foreach (var (mind, member) in collective.Members.ToArray())
        {
            if (member.Pending && !member.Leaving
                && (TerminatingOrDeleted(member.Observer) || !TryComp<MindComponent>(mind, out var consciousness)
                    || consciousness.UserId is not { } user || collective.ExcludedUsers.Contains(user)
                    || !_players.TryGetSessionById(user, out var session) || session.Status != SessionStatus.InGame
                    || session.AttachedEntity != member.Observer))
                RespondJoin(collectiveId, mind, false);
        }
    }

    /// <summary>Revokes membership and applies the configured outcome without changing unrelated roles or objectives.</summary>
    public bool RemoveMember(EntityUid collectiveId, EntityUid mind, bool createVacancy = false,
        MindCollectiveDepartureDisposition? disposition = null)
    {
        if (!TryComp<MindCollectiveComponent>(collectiveId, out var collective)
            || !collective.Members.TryGetValue(mind, out var member) || member.Leaving)
            return false;
        member.Leaving = true;
        try
        {
            RemoveMember(collectiveId, collective, mind, member, createVacancy, disposition);
            return true;
        }
        finally
        {
            member.Leaving = false;
        }
    }

    private void RemoveMember(EntityUid collectiveId, MindCollectiveComponent collective, EntityUid mind,
        MindCollectiveMemberState member, bool createVacancy, MindCollectiveDepartureDisposition? disposition)
    {
        var observer = member.Observer;
        var currentBody = TryComp<MindComponent>(mind, out var departing) ? departing.OwnedEntity : null;
        var leaving = new MindCollectiveMemberLeavingEvent(mind, currentBody, member.PreviousBody,
            disposition ?? (member.Pending ? collective.DeclineDisposition : collective.ExpelDisposition));
        RaiseLocalEvent(collectiveId, leaving);
        RemoveMemberRoles(collectiveId, collective, mind);
        collective.Members.Remove(mind);
        CloseConsent(member);
        if (member.Menu is { IsClosed: false } menu)
            menu.Close();
        member.Menu = null;
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
        }
        if (!TerminatingOrDeleted(mind) && TryComp<MindComponent>(mind, out var consciousness))
        {
            var previous = consciousness.OwnedEntity;
            if (member.Action is { } action && !TerminatingOrDeleted(action))
            {
                _actions.RemoveAction(action);
                QueueDel(action);
            }
            if (TryComp<MindCollectiveMemberComponent>(mind, out var membership) && membership.Collective == collectiveId)
                RemComp<MindCollectiveMemberComponent>(mind);

            if (leaving.Disposition == MindCollectiveDepartureDisposition.ReturnToPreviousBody
                && member.PreviousBody is { } destination && !TerminatingOrDeleted(destination)
                && !HasComp<MindCollectiveObserverComponent>(destination)
                && (!_mind.TryGetMind(destination, out var occupant, out _) || occupant == mind)
                && (!TryComp<ActorComponent>(destination, out var player)
                    || player.PlayerSession.UserId == consciousness.UserId)
                && (!TryGetCollective(destination, out var previousCollective, out _) || previousCollective == collectiveId))
                _mind.TransferTo(mind, destination, ghostCheckOverride: true, createGhost: false, mind: consciousness);

            if (leaving.Disposition == MindCollectiveDepartureDisposition.ReturnToPreviousBody
                && member.PreviousGhostPosition is { } ghostPosition && consciousness.OwnedEntity == observer)
                _ghosts.SpawnGhost((mind, consciousness), ghostPosition, canReturn: false);

            if (leaving.Disposition == MindCollectiveDepartureDisposition.Ghost)
            {
                if (consciousness.VisitingEntity is { } visiting && TryComp<GhostComponent>(visiting, out var ghost))
                    _ghosts.SetCanReturnToBody((visiting, ghost), false);
                var position = previous is { } source && !TerminatingOrDeleted(source)
                    ? Transform(source).Coordinates
                    : !TerminatingOrDeleted(observer) ? Transform(observer).Coordinates : (EntityCoordinates?) null;
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
    }

    private void PublishVacancy(EntityUid observer, MindCollectiveComponent collective)
    {
        if (collective.VacancyPrototype is not { } prototype || TerminatingOrDeleted(observer))
            return;
        collective.VacantObservers.Add(observer);
        EntityManager.AddComponents(observer, _prototypes.Index(prototype), removeExisting: false);
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
        collective.Members.Add(mind, new MindCollectiveMemberState(ent));
        EnsureComp<MindCollectiveMemberComponent>(mind).Collective = collectiveId;
        RemCompDeferred<GhostRoleComponent>(ent);
        FinishJoining(collectiveId, collective, mind, null);
        args.TookRole = true;
    }

    private bool Expel(EntityUid actor, EntityUid targetMind)
    {
        if (!TryGetActorCollective(actor, out var id, out var collective, out var mind)
            || !CanManageMember(mind, targetMind, "mind-collective.expel"))
            return false;
        if (collective.ExcludeExpelledUsers && TryComp<MindComponent>(targetMind, out var consciousness)
            && consciousness.UserId is { } user)
            collective.ExcludedUsers.Add(user);
        return RemoveMember(id, targetMind, createVacancy: collective.CreateVacancyOnExpel,
            disposition: collective.ExpelDisposition);
    }
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
