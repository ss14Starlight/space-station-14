using System.Linq;
using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._Starlight.Changeling;
using Content.Shared.Actions.Components;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private EuiManager _hiveEui = default!;
    [Dependency] private GhostSystem _hiveGhosts = default!;
    [Dependency] private GhostRoleSystem _hiveGhostRoles = default!;

    private static readonly EntProtoId HiveVacancyPrototype = "ChangelingHiveVacancy";
    private readonly Dictionary<EntityUid, AcceptChangelingHiveEui> _hiveConsents = new();

    [SubscribeLocalEvent]
    private void OnHiveRoundRestart(RoundRestartCleanupEvent args)
    {
        var colonies = EntityQueryEnumerator<ChangelingColonyComponent>();
        while (colonies.MoveNext(out _, out var colony))
            colony.PendingMinds.Clear();
        foreach (var mind in _hiveConsents.Keys.ToArray())
            CloseHiveConsent(mind);
    }

    private void RequestHiveConsent(EntityUid master, EntityUid mind, EntityUid anchor, ChangelingColonyComponent colony)
    {
        if (!colony.PendingMinds.Contains(mind) || !colony.Minds.TryGetValue(mind, out var current) || current != anchor
            || _hiveConsents.ContainsKey(mind) || !TryComp<MindComponent>(mind, out var consciousness)
            || consciousness.UserId is not { } user || colony.SilencedUsers.Contains(user)
            || !_player.TryGetSessionById(user, out var session) || session.Status != SessionStatus.InGame
            || session.AttachedEntity != anchor)
            return;

        var eui = new AcceptChangelingHiveEui(master, mind, this);
        _hiveConsents.Add(mind, eui);
        _hiveEui.OpenEui(eui, session);
    }

    private void CloseHiveConsent(EntityUid mind)
    {
        if (_hiveConsents.Remove(mind, out var eui) && !eui.IsClosed && !eui.IsShutDown)
            eui.Close();
    }

    /// <summary>
    /// Resolves an absorbed player's pending choice once. Refusal ends their current membership without returning their body.
    /// </summary>
    public void RespondHiveConsent(Entity<ChangelingColonyComponent?> master, EntityUid mind, bool accepted)
    {
        if (mind == master.Owner || !Resolve(master, ref master.Comp, false) || !master.Comp.PendingMinds.Remove(mind))
            return;
        var colony = master.Comp;
        CloseHiveConsent(mind);
        if (!colony.Minds.TryGetValue(mind, out var anchor) || TerminatingOrDeleted(anchor)
            || !TryComp<MindComponent>(mind, out var consciousness))
            return;

        if (!accepted || consciousness.OwnedEntity != anchor || consciousness.UserId is not { } user
            || colony.SilencedUsers.Contains(user) || !_player.TryGetSessionById(user, out var session)
            || session.Status != SessionStatus.InGame || session.AttachedEntity != anchor)
        {
            ExpelHiveMind(master.Owner, mind, colony, createVacancy: true);
            return;
        }

        foreach (var objective in consciousness.Objectives.ToArray())
            _mind.TryRemoveObjective(mind, consciousness, objective);
        if (!consciousness.MindRoleContainer.ContainedEntities.Any(role => !TerminatingOrDeleted(role)
                && MetaData(role).EntityPrototype?.ID == "MindRoleChangelingHive"))
            _changelingRoles.MindAddRole(mind, "MindRoleChangelingHive", consciousness, silent: true);
        FinishJoiningHive(master.Owner, mind, anchor, colony);
    }

    private void FinishJoiningHive(EntityUid master, EntityUid mind, EntityUid anchor, ChangelingColonyComponent colony)
    {
        SyncHiveActions(master, mind);
        RefreshFragmentActions(anchor, colony);
        var observer = Comp<ChangelingMindspaceComponent>(anchor);
        UpdateHiveObserver(anchor, observer, colony);
        _popup.PopupEntity(Loc.GetString("changeling-hive-absorbed"), anchor, anchor);
        UpdateColonyUi(anchor, colony);
        observer.OpenUiAt = _timing.CurTime + TimeSpan.FromMilliseconds(100);
    }

    [SubscribeLocalEvent]
    private void OnSilenceHiveMind(EntityUid uid, ChangelingColonyMemberComponent member, ChangelingSilenceMessage args)
    {
        if (args.Actor != uid || !IsHiveMaster(uid, member)
            || !TryComp<ChangelingColonyComponent>(member.Mind, out var colony))
            return;
        var mind = GetEntity(args.Mind);
        if (mind == member.Mind || !colony.Minds.ContainsKey(mind))
            return;
        if (TryComp<MindComponent>(mind, out var consciousness) && consciousness.UserId is { } user)
            colony.SilencedUsers.Add(user);

        _hiveLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(uid):player} expelled {ToPrettyString(mind):entity} from their hive");
        ExpelHiveMind(member.Mind, mind, colony, createVacancy: false);
        UpdateColonyUi(uid, colony);
    }

    private void ExpelHiveMind(EntityUid master, EntityUid mind, ChangelingColonyComponent colony, bool createVacancy)
    {
        if (mind == master || !colony.Minds.Remove(mind, out var anchor))
            return;
        colony.PendingMinds.Remove(mind);
        CloseHiveConsent(mind);

        if (TryComp<MindComponent>(mind, out var consciousness))
        {
            var body = consciousness.OwnedEntity;
            if (body is { } previous && !TerminatingOrDeleted(previous))
            {
                if (TryComp<ChangelingColonyMemberComponent>(previous, out var member))
                    EndCapture(previous, member);
                StopHiveBody(previous);
            }
            if (TryComp<ActionsContainerComponent>(mind, out var actions))
            {
                foreach (var action in actions.Container.ContainedEntities.ToArray())
                {
                    colony.DisabledActions.Remove(action);
                    if (TerminatingOrDeleted(action))
                        continue;
                    _actions.RemoveAction(action);
                    QueueDel(action);
                }
            }
            if (consciousness.MindRoleContainer.ContainedEntities.Any(role => !TerminatingOrDeleted(role)
                    && MetaData(role).EntityPrototype?.ID == "MindRoleChangelingHive"))
                _changelingRoles.MindRemoveRole((mind, consciousness), "MindRoleChangelingHive");

            // A visiting ghost must also lose its return path before the forced transfer.
            if (consciousness.VisitingEntity is { } visiting && TryComp<GhostComponent>(visiting, out var ghost))
                _hiveGhosts.SetCanReturnToBody((visiting, ghost), false);
            var position = !TerminatingOrDeleted(anchor) ? Transform(anchor).Coordinates : (EntityCoordinates?) null;
            var observer = _hiveGhosts.SpawnGhost((mind, consciousness), position, canReturn: false);
            if (!createVacancy && observer is { } expelled)
                _popup.PopupEntity(Loc.GetString("changeling-hive-silenced"), expelled, expelled);
            if (body is { } vacant && colony.Members.Contains(vacant) && !TerminatingOrDeleted(vacant))
                ActivateVacantCarrier(vacant);
        }

        if (TerminatingOrDeleted(anchor))
            return;
        if (createVacancy)
            PublishHiveVacancy(anchor, colony);
        else
        {
            colony.VacantAnchors.Remove(anchor);
            QueueDel(anchor);
        }
    }

    private void PublishHiveVacancy(EntityUid anchor, ChangelingColonyComponent colony)
    {
        colony.VacantAnchors.Add(anchor);
        // The template supplies role metadata without overriding the mindspace's existing components.
        EntityManager.AddComponents(anchor, _proto.Index(HiveVacancyPrototype), removeExisting: false);
    }

    [SubscribeLocalEvent]
    private void OnRequestHiveRole(Entity<ChangelingMindspaceComponent> ent, ref GhostRoleRequestAttemptEvent args)
    {
        args.Cancelled |= !TryComp<ChangelingColonyComponent>(ent.Comp.Colony, out var colony)
                          || !colony.VacantAnchors.Contains(ent) || colony.SilencedUsers.Contains(args.Player.UserId);
    }

    [SubscribeLocalEvent]
    private void OnTakeHiveRole(Entity<ChangelingMindspaceComponent> ent, ref TakeGhostRoleEvent args)
    {
        var master = ent.Comp.Colony;
        if (args.TookRole || TerminatingOrDeleted(ent) || !TryComp<ChangelingColonyComponent>(master, out var colony)
            || !colony.Core.IsValid() || TerminatingOrDeleted(colony.Core) || !colony.VacantAnchors.Contains(ent)
            || !colony.Members.Any(CanEnterHiveCarrier) || colony.SilencedUsers.Contains(args.Player.UserId)
            || args.Player.Status != SessionStatus.InGame || !HasComp<GhostComponent>(args.Player.AttachedEntity)
            || _mind.TryGetMind(ent, out _, out _) || !TryComp<GhostRoleComponent>(ent, out var role) || role.Taken)
            return;
        if (_mind.TryGetMind(args.Player.UserId, out var previous, out _) && previous is { } old && colony.Minds.ContainsKey(old))
            return;

        colony.VacantAnchors.Remove(ent);
        _hiveGhostRoles.MarkGhostRoleTaken((ent.Owner, role));
        _hiveGhostRoles.GhostRoleInternalCreateMindAndTransfer(args.Player, ent, ent, role);
        if (!_mind.TryGetMind(ent, out var mind, out var consciousness) || consciousness.UserId != args.Player.UserId)
        {
            RemComp<GhostRoleComponent>(ent);
            PublishHiveVacancy(ent, colony);
            return;
        }

        colony.Minds.Add(mind, ent);
        RemCompDeferred<GhostRoleComponent>(ent);
        FinishJoiningHive(master, mind, ent, colony);
        args.TookRole = true;
    }
}
