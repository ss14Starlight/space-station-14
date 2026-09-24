using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Server._Starlight.AlertArmory;
using Content.Server._Starlight.Statistics;
using Content.Shared.Access.Systems;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Content.Shared._Starlight.SecureTerminal;
using Content.Server.GameTicking.Rules.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Server.Radio.EntitySystems;
using Content.Server.Mind;
using Content.Server.Chat.Managers;
using Content.Shared.Roles.Jobs;
using Content.Server.Administration;
using Content.Server.EUI;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Content.Server.Nuke;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Toggleable;
using Content.Server._Starlight.Administration.Systems;
using Content.Shared._NullLink;
using Content.Shared._Starlight.Computers.PodConsole;

namespace Content.Server._Starlight.SecureTerminal;

/// <summary>
/// Drives the Secure Command Terminal — proposal creation, multi-party authorization,
/// countdown timers, fee deduction, salary penalties, and final action execution.
/// </summary>
public sealed partial class SecureCommandTerminalSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private StationSystem _stations = default!;
    [Dependency] private AlertLevelSystem _alertLevel = default!;
    [Dependency] private AlertArmorySystem _armory = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IPrototypeManager _protos = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private QuickDialogSystem _quickDialog = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private NukeCodePaperSystem _nukeCodeSystem = default!;
    [Dependency] private SharedAirlockSystem _airlock = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AutoDiscordLogSystem _autolog = default!;
    [Dependency] private RoundStatisticsSystem _roundStatistics = default!;
    [Dependency] private ISharedNullLinkPlayerResourcesManager _playerResources = default!;
    [Dependency] private EuiManager _euiManager = default!;

    private readonly Dictionary<(EntityUid StationUid, string RequestId), HashSet<SecureTerminalAdminApprovalEui>> _adminApprovalEuis = new();

    public override void Initialize()
    {
        Subs.BuiEvents<SecureCommandTerminalConsoleComponent>(SecureCommandTerminalUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<SecureTerminalRequestMessage>(OnRequest);
            subs.Event<SecureTerminalAuthorizeMessage>(OnAuthorize);
            subs.Event<SecureTerminalDenyMessage>(OnDeny);
            subs.Event<SecureTerminalRecallMessage>(OnRecall);
        });
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
    }

    private void OnAlertLevelChanged(AlertLevelChangedEvent ev)
    {
        if (!TryComp<SecureCommandTerminalStationComponent>(ev.Station, out var stationComp))
            return;

        stationComp.AlertLevelSetAt = _timing.CurTime;
        UpdateAllConsolesForStation(ev.Station);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var stationQuery = EntityQueryEnumerator<SecureCommandTerminalStationComponent>();
        while (stationQuery.MoveNext(out var stationUid, out var stationComp))
        {
            // Remove expired cooldowns
            List<string>? expiredKeys = null;
            foreach (var (key, endTime) in stationComp.Cooldowns)
            {
                if (endTime <= now)
                    (expiredKeys ??= new()).Add(key);
            }
            if (expiredKeys != null)
                foreach (var k in expiredKeys)
                    stationComp.Cooldowns.Remove(k);

            // Fire activating proposals whose timer has elapsed.
            List<string>? toFire = null;
            List<string>? toCancel = null;
            List<EntityUid>? authToLightUp = null;
            foreach (var (requestId, proposal) in stationComp.ActiveProposals)
            {
                if (!_protos.TryIndex<SecureCommandTerminalRequestPrototype>(requestId, out var activeProto))
                    continue;

                if (proposal.Status == SecureTerminalProposalStatus.Activating &&
                    activeProto.VetoSchemes.Count > 0 && HasLostVetoPresence(proposal))
                    RemoveAbsentVetoers(proposal);

                if (proposal.Status == SecureTerminalProposalStatus.Activating &&
                    proposal.ActivateAt > now && activeProto.VetoSchemes.Count > 0 &&
                    HasVeto(proposal, activeProto))
                {
                    (toCancel ??= new()).Add(requestId);
                    continue;
                }

                if (proposal.Status == SecureTerminalProposalStatus.Pending)
                {
                    if (!IsRequesterPresent(proposal))
                    {
                        (toCancel ??= new()).Add(requestId);
                        continue;
                    }

                    if (HasLostAuthorizationPresence(proposal))
                        RemoveAbsentAuthorizers(proposal);

                    if (proposal.Authorizers.Count == 0)
                    {
                        (toCancel ??= new()).Add(requestId);
                        continue;
                    }

                    var query = EntityQueryEnumerator<SecureCommandTerminalConsoleComponent>();
                    while (query.MoveNext(out var consoleUid, out var comp))
                        if (_stations.GetOwningStation(consoleUid) == stationUid && comp.AuthTerminal && !proposal.UsedTerminals.Contains(consoleUid))
                            (authToLightUp ??= new()).Add(consoleUid);
                }

                if (proposal.Status == SecureTerminalProposalStatus.Activating &&
                    proposal.ActivateAt.HasValue && proposal.ActivateAt.Value <= now)
                    (toFire ??= new()).Add(requestId);
            }

            if (authToLightUp != null)
                foreach (var consoleUid in authToLightUp)
                    _appearance.SetData(consoleUid, ToggleableVisuals.Enabled, true);

            var query2 = EntityQueryEnumerator<SecureCommandTerminalConsoleComponent>();
            while (query2.MoveNext(out var consoleUid, out var comp))
            {
                if (!comp.AuthTerminal || _stations.GetOwningStation(consoleUid) != stationUid)
                    continue;

                _appearance.SetData(consoleUid,
                    ToggleableVisuals.Enabled,
                    authToLightUp is not null && authToLightUp.Contains(consoleUid));
            }

            if (toFire != null)
                foreach (var requestId in toFire)
                {
                    if (_protos.TryIndex<SecureCommandTerminalRequestPrototype>(requestId, out var proto))
                    {
                        // grab the requester before the proposal is removed incase of recall
                        var requester = stationComp.ActiveProposals.TryGetValue(requestId, out var firingProposal)
                            ? firingProposal.Requester
                            : EntityUid.Invalid;

                        // Salery will be modified on activation now, now longer on accepting proposal, so veto is not a way to avoid the penalty
                        foreach (var modifier in proto.SalaryModifiers)
                            stationComp.SalaryModifiers[modifier.Source] = stationComp.SalaryModifiers.GetValueOrDefault(modifier.Source) + modifier.Change;

                        ExecuteAction(stationUid, proto);
                        _roundStatistics.RecordSecureTerminalOutcome(requestId, proto.ActionType, SecureTerminalResult.Executed);
                        stationComp.ActiveProposals.Remove(requestId);
                        if (proto.ActionType == SecureTerminalActionType.Armory)
                        {
                            // Track as deployed so it can still be recalled
                            var authorizedAt = now - TimeSpan.FromSeconds(proto.ActivationDelaySecs);
                            stationComp.DeployedArmories[requestId] = authorizedAt;
                            stationComp.DeployedArmoryRequesters[requestId] = requester;
                        }
                        else if (proto.OneTimeUse)
                            stationComp.UsedOnce.Add(requestId);
                        else
                            stationComp.Cooldowns[requestId] = now + TimeSpan.FromSeconds(proto.CooldownSecs);
                    }
                    else
                    {
                        stationComp.ActiveProposals.Remove(requestId);
                        stationComp.Cooldowns[requestId] = now + TimeSpan.FromSeconds(1800);
                    }
                }

            if (toCancel != null)
                foreach (var requestId in toCancel)
                {
                    if (!stationComp.ActiveProposals.TryGetValue(requestId, out var cancelledProposal))
                        continue;

                    CancelProposal(stationUid, stationComp, requestId, cancelledProposal,
                        cancelledProposal.RequesterTerminal, EntityUid.Invalid, false);
                }

            UpdateAllConsolesForStation(stationUid);
        }
    }

    private void OnUiOpened(EntityUid uid, SecureCommandTerminalConsoleComponent comp, BoundUIOpenedEvent ev)
    {
        if (!comp.Enabled)
        {
            _ui.CloseUi(uid, SecureCommandTerminalUiKey.Key, ev.Actor);
            return;
        }

        UpdateConsoleInterface(uid);
    }

    private void OnRequest(EntityUid uid, SecureCommandTerminalConsoleComponent comp, SecureTerminalRequestMessage msg)
    {
        if (!comp.Enabled) return;
        var actor = msg.Actor;
        if (!actor.IsValid()) return;

        if (!_protos.TryIndex<SecureCommandTerminalRequestPrototype>(msg.RequestId, out var proto))
            return;

        var stationUid = _stations.GetOwningStation(uid);
        if (stationUid == null)
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-no-station"), actor);
            return;
        }

        if (!TryComp<SecureCommandTerminalStationComponent>(stationUid.Value, out var stationComp))
            return;

        if (comp.AuthTerminal)
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-auth-note"), actor, PopupType.MediumCaution);
            return;
        }

        // Only someone who can satisfy at least one auth group may create a proposal
        if (!CanRequest(actor, proto))
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-request-denied"), actor, PopupType.Medium);
            return;
        }

        // Reason Check
        if (proto.RequireReason)
        {
            if (!TryComp<ActorComponent>(actor, out var actorcomp) || actorcomp.PlayerSession is null)
                return;

            _quickDialog.OpenDialog(actorcomp.PlayerSession, Loc.GetString("secure-terminal-reason"), "",
            (string message) =>
            {
                if (actorcomp.PlayerSession is null)
                    return;

                CreateProposal(uid, msg, actor, stationUid.Value, stationComp, proto, comp, message);
            }, () =>
            {
                return;
            });
            return;
        }
        else
            CreateProposal(uid, msg, actor, stationUid.Value, stationComp, proto, comp);
    }

    private void CreateProposal(EntityUid uid, SecureTerminalRequestMessage msg, EntityUid actor, EntityUid stationUid, SecureCommandTerminalStationComponent stationComp, SecureCommandTerminalRequestPrototype proto, SecureCommandTerminalConsoleComponent comp, string? reason = null)
    {
        // Condition checks
        if (proto.RequiresWarDeclared && !IsWarDeclared())
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-requires-war"), actor, PopupType.Medium);
            return;
        }
        else if (proto.RequiresWarNotDeclared && IsWarDeclared())
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-requires-no-war-note"), actor, PopupType.Medium);
            return;
        }

        if (proto.RequiresAlertLevel != null &&
            TryComp<AlertLevelComponent>(stationUid, out var alertComp) &&
            alertComp.CurrentLevel != proto.RequiresAlertLevel)
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-wrong-alert"), actor, PopupType.Medium);
            return;
        }

        if (proto.RequiresAlertActiveMinutes > 0 &&
            (_timing.CurTime - stationComp.AlertLevelSetAt).TotalMinutes < proto.RequiresAlertActiveMinutes)
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-alert-not-long-enough"), actor, PopupType.Medium);
            return;
        }

        if (stationComp.Cooldowns.ContainsKey(msg.RequestId))
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-on-cooldown"), actor, PopupType.Medium);
            return;
        }

        if (stationComp.UsedOnce.Contains(msg.RequestId))
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-already-used"), actor, PopupType.Medium);
            return;
        }

        if (stationComp.ActiveProposals.ContainsKey(msg.RequestId))
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-already-pending"), actor, PopupType.Medium);
            return;
        }

        if (stationComp.DeployedArmories.ContainsKey(msg.RequestId))
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-used-note"), actor, PopupType.Medium);
            return;
        }

        // Only one active proposal at a time
        if (stationComp.ActiveProposals.Count > 0)
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-already-active"), actor, PopupType.Medium);
            return;
        }

        // We charge charge the requester when requested so we don't have to deal with them not having the funds when approved.
        if (proto.Fee > 0)
        {
            if (_playerResources.TryGetResource(actor, "credits", out var balance) && balance < proto.Fee)
            {
                _popup.PopupCursor($"Insufficient funds. Required: {proto.Fee}\u20a1", actor, PopupType.Medium);
                return;
            }

            _playerResources.TryUpdateResource(actor, "credits", -proto.Fee);
            _popup.PopupCursor($"Held {proto.Fee}\u20a1 pending authorization.", actor, PopupType.Medium);
        }

        // Create the proposal
        var proposal = new SecureTerminalProposalData
        {
            RequestId = msg.RequestId,
            Requester = actor,
            RequesterTerminal = uid,
            CreatedAt = _timing.CurTime
        };
        stationComp.ActiveProposals[msg.RequestId] = proposal;

        _roundStatistics.RecordSecureTerminalProposal(msg.RequestId, proto.ActionType, reason is not null, proto.Fee);

        if (reason is not null)
            proposal.Reason = reason;

        // The requestor automatically authorizes their matching group(s)
        TryAuthorize(actor, proposal, proto, uid, comp);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(actor):player} created secure terminal proposal: {msg.RequestId}");

        _autolog.LogToDiscord($"created secure terminal proposal: {msg.RequestId}", ToPrettyString(actor));

        _chatManager.SendAdminAnnouncement(
            $"Secure Terminal — {MetaData(actor).EntityName} ({GetJobName(actor)}) proposed: {Loc.GetString(proto.Name)}.");

        CheckAndStartCountdown(stationUid, stationComp, msg.RequestId, proto);

        if (proposal.Status == SecureTerminalProposalStatus.Pending)
        {
            var proposalAnnounce = Loc.GetString("secure-terminal-proposal-created", ("request", Loc.GetString(proto.Name)));
            if (reason is not null)
                proposalAnnounce = Loc.GetString("secure-terminal-proposal-created-reason", ("request", Loc.GetString(proto.Name)), ("reason", reason));

            if (proto.ProposalAnnouncement)
                _chat.DispatchGlobalAnnouncement(proposalAnnounce, colorOverride: proto.AnnouncementColor);

            var proposalRadio = Loc.GetString("secure-terminal-radio-proposal", ("request", Loc.GetString(proto.Name)));
            if (reason is not null)
                proposalRadio = Loc.GetString("secure-terminal-radio-proposal-reason", ("request", Loc.GetString(proto.Name)), ("reason", reason));

            _radio.SendRadioMessage(uid, proposalRadio, "Command", uid);
        }

        UpdateAllConsolesForStation(stationUid);
    }

    private void OnAuthorize(EntityUid uid, SecureCommandTerminalConsoleComponent comp, SecureTerminalAuthorizeMessage msg)
    {
        var actor = msg.Actor;
        if (!actor.IsValid()) return;

        if (!_protos.TryIndex<SecureCommandTerminalRequestPrototype>(msg.RequestId, out var proto))
            return;

        var stationUid = _stations.GetOwningStation(uid);
        if (stationUid == null) return;
        if (!TryComp<SecureCommandTerminalStationComponent>(stationUid.Value, out var stationComp)) return;

        if (!stationComp.ActiveProposals.TryGetValue(msg.RequestId, out var proposal) ||
            proposal.Status != SecureTerminalProposalStatus.Pending)
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-no-active-proposal"), actor, PopupType.Medium);
            return;
        }

        if (comp.Admin)
        {
            proposal.AdminApproved = true;
            _roundStatistics.RecordSecureTerminalAuthorization(msg.RequestId, proto.ActionType, true);
            _autolog.LogToDiscord($"authorized secure terminal proposal: {msg.RequestId}", ToPrettyString(actor));
        }
        if (!comp.Admin && proposal.UsedTerminals.Contains(uid))
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-already-activated"), actor, PopupType.Medium);
            return;
        }

        if (!TryAuthorize(actor, proposal, proto, uid, comp))
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-authorize-denied"), actor, PopupType.Medium);
            return;
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(actor):player} authorized secure terminal proposal: {msg.RequestId}");

        _chatManager.SendAdminAnnouncement(
            $"Secure Terminal — {MetaData(actor).EntityName} ({GetJobName(actor)}) authorized: {Loc.GetString(proto.Name)}.");

        CheckAndStartCountdown(stationUid.Value, stationComp, msg.RequestId, proto);
        UpdateAllConsolesForStation(stationUid.Value);
    }

    private void OnDeny(EntityUid uid, SecureCommandTerminalConsoleComponent comp, SecureTerminalDenyMessage msg)
    {
        var actor = msg.Actor;
        if (!actor.IsValid()) return;

        var stationUid = _stations.GetOwningStation(uid);
        if (stationUid == null) return;
        if (!TryComp<SecureCommandTerminalStationComponent>(stationUid.Value, out var stationComp)) return;

        if (!stationComp.ActiveProposals.TryGetValue(msg.RequestId, out var deniedProposal))
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-no-active-proposal"), actor, PopupType.Medium);
            return;
        }

        if (!_protos.TryIndex<SecureCommandTerminalRequestPrototype>(msg.RequestId, out var proto))
            return;

        if (deniedProposal.Status == SecureTerminalProposalStatus.Activating)
        {
            if (deniedProposal.UsedVetoTerminals.Contains(uid))
                return;
            _roundStatistics.RecordSecureTerminalOutcome(msg.RequestId, proto.ActionType, SecureTerminalResult.Denied);

            _chatManager.SendAdminAnnouncement(
                $"Secure Terminal — {MetaData(actor).EntityName} ({GetJobName(actor)}) DENIED / cancelled: {Loc.GetString(proto.Name)}.");

            if (proto.ProposalAnnouncement)
            {
                _popup.PopupCursor(Loc.GetString("secure-terminal-already-activated"), actor, PopupType.Medium);
                return;
            }

            if (!deniedProposal.ActivateAt.HasValue || deniedProposal.ActivateAt.Value <= _timing.CurTime || proto.VetoSchemes.Count == 0 ||
                !TryVeto(actor, deniedProposal, proto, uid))
                _popup.PopupCursor(Loc.GetString("secure-terminal-request-denied"), actor, PopupType.Medium);
            else if (HasVeto(deniedProposal, proto))
                CancelProposal(stationUid.Value, stationComp, msg.RequestId, deniedProposal, uid, actor, comp.Admin, true);
            return;
        }

        var tags = _access.FindAccessTags(actor);
        if (!tags.Contains((Robust.Shared.Prototypes.ProtoId<Content.Shared.Access.AccessLevelPrototype>)"Command"))
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-request-denied"), actor, PopupType.Medium);
            return;
        }

        CancelProposal(stationUid.Value, stationComp, msg.RequestId, deniedProposal, uid, actor, comp.Admin);
    }

    private void OnRecall(EntityUid uid, SecureCommandTerminalConsoleComponent _, SecureTerminalRecallMessage msg)
    {
        var actor = msg.Actor;
        if (!actor.IsValid()) return;

        var stationUid = _stations.GetOwningStation(uid);
        if (stationUid == null) return;
        if (!TryComp<SecureCommandTerminalStationComponent>(stationUid.Value, out var stationComp)) return;

        if (!_protos.TryIndex<SecureCommandTerminalRequestPrototype>(msg.RequestId, out var proto) ||
        proto.ActionType != SecureTerminalActionType.Armory)
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-no-active-proposal"), actor, PopupType.Medium);
            return;
        }

        var hasActivatingArmory =
            stationComp.ActiveProposals.TryGetValue(msg.RequestId, out var proposal) &&
            proposal.Status == SecureTerminalProposalStatus.Activating;

        var hasDeployedArmory = stationComp.DeployedArmories.ContainsKey(msg.RequestId);

        if (!hasActivatingArmory && !hasDeployedArmory)
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-no-active-proposal"), actor, PopupType.Medium);
            return;
        }

        var tags = _access.FindAccessTags(actor);
        if (!tags.Contains((Robust.Shared.Prototypes.ProtoId<Content.Shared.Access.AccessLevelPrototype>)"Command")) // Your serious?
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-request-denied"), actor, PopupType.Medium);
            return;
        }

        if (proto.RecallMinDelaySecs > 0)
        {
            TimeSpan authorizedAt;
            if (proposal != null && proposal.ActivateAt.HasValue)
                authorizedAt = proposal.ActivateAt.Value - TimeSpan.FromSeconds(proto.ActivationDelaySecs);
            else if (stationComp.DeployedArmories.TryGetValue(msg.RequestId, out var deployedAuthorizedAt))
                authorizedAt = deployedAuthorizedAt;
            else
                authorizedAt = TimeSpan.Zero;

            var recallAvailableAt = authorizedAt + TimeSpan.FromSeconds(proto.RecallMinDelaySecs);
            if (_timing.CurTime < recallAvailableAt)
            {
                _popup.PopupCursor(Loc.GetString("secure-terminal-recall-too-soon"), actor, PopupType.Medium);
                return;
            }
        }

        // Gonna keep the code here, becuase I put in the time to write it and get it working,
        // but if an armory is called it is probably going to arrive.
        // And it can be recalled once on station so it dosen't make much sense for it to be refunded.
        // Refund if armory is recalled
        var refundTarget = hasActivatingArmory && proposal != null
            ? proposal.Requester
            : stationComp.DeployedArmoryRequesters.GetValueOrDefault(msg.RequestId, EntityUid.Invalid);

        // Attempt to recall the shuttle. If the recall fails abort the terminal state update.
        if (proto.ArmoryKey != null && !_armory.RecallArmory(stationUid.Value, proto.ArmoryKey))
            return;

        stationComp.ActiveProposals.Remove(msg.RequestId);
        stationComp.DeployedArmories.Remove(msg.RequestId);
        stationComp.DeployedArmoryRequesters.Remove(msg.RequestId);
        stationComp.UsedOnce.Add(msg.RequestId);

        _roundStatistics.RecordSecureTerminalOutcome(msg.RequestId, proto.ActionType, SecureTerminalResult.Recalled);

        RefundFee(refundTarget, proto, 0f);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(actor):player} recalled armory via secure terminal: {msg.RequestId}");

        if (_protos.TryIndex(msg.RequestId, out proto))
        {
            _chatManager.SendAdminAnnouncement(
                $"Secure Terminal — {MetaData(actor).EntityName} ({GetJobName(actor)}) RECALLED: {Loc.GetString(proto.Name)}.");

            _chat.DispatchGlobalAnnouncement(
                Loc.GetString("secure-terminal-armory-recalled",
                    ("request", Loc.GetString(proto.Name))),
                colorOverride: proto.AnnouncementColor);

            _radio.SendRadioMessage(uid,
                Loc.GetString("secure-terminal-armory-recalled",
                    ("request", Loc.GetString(proto.Name))),
                "Command", uid);
        }

        UpdateAllConsolesForStation(stationUid.Value);
    }

    private void OpenAdminApprovalEuis(EntityUid stationUid, string requestId, SecureCommandTerminalRequestPrototype proto,
        SecureTerminalProposalData proposal)
    {
        if (!proto.RequiresAdminApproval)
            return;

        var key = (stationUid, requestId);
        if (_adminApprovalEuis.ContainsKey(key))
            return;

        foreach (var admin in _adminManager.ActiveAdmins)
        {
            var eui = new SecureTerminalAdminApprovalEui(this, stationUid, requestId, Loc.GetString(proto.Name),
                Loc.GetString(proto.Description),
                string.IsNullOrWhiteSpace(proposal.Reason) ? null : proposal.Reason,
                proposal.Authorizers
                    .Select(authorizer => $"{authorizer.Name} ({authorizer.Job})")
                    .Distinct()
                    .ToList());
            if (!_adminApprovalEuis.TryGetValue(key, out var euis))
            {
                euis = new HashSet<SecureTerminalAdminApprovalEui>();
                _adminApprovalEuis[key] = euis;
            }

            euis.Add(eui);
            _euiManager.OpenEui(eui, admin);
        }
    }

    public void OnAdminApprovalEuiClosed(SecureTerminalAdminApprovalEui eui)
    {
        var key = (eui.StationUid, eui.RequestId);
        if (!_adminApprovalEuis.TryGetValue(key, out var euis))
            return;

        euis.Remove(eui);
        if (euis.Count == 0)
            _adminApprovalEuis.Remove(key);
    }

    private void CloseAdminApprovalEuis(EntityUid stationUid, string requestId)
    {
        if (!_adminApprovalEuis.Remove((stationUid, requestId), out var euis))
            return;

        foreach (var eui in euis)
        {
            if (!eui.IsShutDown)
                eui.Close();
        }
    }

    public void HandleAdminApproval(ICommonSession admin, EntityUid stationUid, string requestId, bool approved)
    {
        if (!_adminManager.ActiveAdmins.Any(session => session.UserId == admin.UserId))
            return;

        if (!TryComp<SecureCommandTerminalStationComponent>(stationUid, out var stationComp) ||
            !stationComp.ActiveProposals.TryGetValue(requestId, out var proposal) ||
            proposal.Status != SecureTerminalProposalStatus.Pending ||
            !_protos.TryIndex<SecureCommandTerminalRequestPrototype>(requestId, out var proto))
            return;

        if (approved)
        {
            proposal.AdminApproved = true;
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{admin.Name} approved secure terminal proposal: {requestId}");
            CheckAndStartCountdown(stationUid, stationComp, requestId, proto);
        }
        else
        {
            CancelProposal(stationUid, stationComp, requestId, proposal, EntityUid.Invalid,
                admin.AttachedEntity ?? EntityUid.Invalid, true);
        }

        CloseAdminApprovalEuis(stationUid, requestId);

        UpdateAllConsolesForStation(stationUid);
    }

    /// <summary>
    /// Attempts to satisfy one available auth group in every scheme for <paramref name="actor"/>.
    /// Returns true if at least one unsatisfied group was claimed.
    /// </summary>
    private bool TryAuthorize(EntityUid actor, SecureTerminalProposalData proposal,
        SecureCommandTerminalRequestPrototype proto, EntityUid terminalUid, SecureCommandTerminalConsoleComponent terminal)
    {
        if (terminal.Admin)
            return true;

        var accessTags = _access.FindAccessTags(actor);
        var schemes = proto.AuthSchemes;

        var authorized = false;
        for (var schemeIndex = 0; schemeIndex < schemes.Count; schemeIndex++)
        {
            var scheme = schemes[schemeIndex];
            if (proposal.Authorizers.Any(a => a.PlayerUid == actor && a.SchemeIndex == schemeIndex))
                continue;

            var satisfied = BuildSatisfiedGroups(proposal, schemeIndex, scheme.Groups.Count);
            for (var groupIndex = 0; groupIndex < scheme.Groups.Count; groupIndex++)
            {
                if (satisfied[groupIndex]) continue;
                var group = scheme.Groups[groupIndex];
                if (!group.Any(tag => accessTags.Contains(tag)))
                    continue;

                string name, job;
                if (_idCard.TryFindIdCard(actor, out var vetoIdCard))
                {
                    name = vetoIdCard.Comp.FullName ?? MetaData(actor).EntityName;
                    job = vetoIdCard.Comp.LocalizedJobTitle ?? GetJobName(actor);
                }
                else
                {
                    name = MetaData(actor).EntityName;
                    job = GetJobName(actor);
                }
                proposal.UsedTerminals.Add(terminalUid);
                proposal.Authorizers.Add((actor, name, job, terminalUid, schemeIndex, groupIndex));
                _roundStatistics.RecordSecureTerminalAuthorization(proposal.RequestId, proto.ActionType, false);
                authorized = true;
                break;
            }
        }
        return authorized;
    }

    /// <summary>
    /// If all auth groups are satisfied, begin the countdown and charge the fee.
    /// </summary>
    private void CheckAndStartCountdown(EntityUid stationUid,
        SecureCommandTerminalStationComponent stationComp,
        string requestId, SecureCommandTerminalRequestPrototype proto)
    {
        if (!stationComp.ActiveProposals.TryGetValue(requestId, out var proposal)) return;
        if (proposal.Status != SecureTerminalProposalStatus.Pending) return;

        var schemeSatisfied = proto.AuthSchemes
            .Select((scheme, schemeIndex) => BuildSatisfiedGroups(proposal, schemeIndex, scheme.Groups.Count))
            .Any(satisfiedGroups => satisfiedGroups.All(satisfied => satisfied));
        if (!schemeSatisfied) return;

        var adminApprovalBypassed = false;

        // Admin Approval
        if (proto.RequiresAdminApproval && !proposal.AdminApproved)
        {
            if (_adminManager.ActiveAdmins.Count() > 0 || !proto.BypassIfNoAdmin)
            {
                OpenAdminApprovalEuis(stationUid, requestId, proto, proposal);
                _chat.DispatchGlobalAnnouncement(Loc.GetString("secure-terminal-awaiting-admin", ("request", Loc.GetString(proto.Name))), colorOverride: proto.AnnouncementColor);
                _chatManager.SendAdminAlert(Loc.GetString("secure-terminal-admin", ("request", Loc.GetString(proto.Name)), ("reason", proposal.Reason)));
                _audio.PlayGlobal("/Audio/Misc/adminlarm.ogg",
                    Filter.Empty().AddPlayers(_adminManager.ActiveAdmins),
                    false,
                    AudioParams.Default.WithVolume(-8f));
                return;
            }

            adminApprovalBypassed = true;
        }

        _autolog.LogToDiscord($"activating secure terminal proposal: {requestId}");

        proposal.Status = SecureTerminalProposalStatus.Activating;
        proposal.ActivateAt = _timing.CurTime + TimeSpan.FromSeconds(proto.ActivationDelaySecs);

        var salaryPenalty = proto.SalaryModifiers
            .Where(modifier => modifier.Change < 0)
            .Sum(modifier => -modifier.Change);
        _roundStatistics.RecordSecureTerminalActivation(requestId, proto.ActionType, _timing.CurTime - proposal.CreatedAt, salaryPenalty);

        if (proto.ProposalAnnouncement)
        {
            // Authorized-by announcement listing signatories from the scheme that passed.
            var authorizedSchemeIndex = proto.AuthSchemes
                .Select((scheme, schemeIndex) => (scheme, schemeIndex))
                .FirstOrDefault(item => BuildSatisfiedGroups(proposal, item.schemeIndex, item.scheme.Groups.Count)
                    .All(satisfied => satisfied))
                .schemeIndex;
            var signatories = string.Join(", ",
                proposal.Authorizers
                    .Where(authorizer => authorizer.SchemeIndex == authorizedSchemeIndex)
                    .GroupBy(authorizer => authorizer.PlayerUid)
                    .Select(group => group.First())
                    .Select(authorizer => $"{authorizer.Name} ({authorizer.Job})"));
            var authorizationMessage = Loc.GetString("secure-terminal-authorized-by",
                ("request", Loc.GetString(proto.Name)),
                ("signatories", signatories));
            if (proto.RequiresAdminApproval)
            {
                var approvalNote = adminApprovalBypassed
                    ? "secure-terminal-authorized-by-central-command-deferred"
                    : "secure-terminal-authorized-by-central-command";
                authorizationMessage += $" {Loc.GetString(approvalNote, ("request", Loc.GetString(proto.Name)))}";
            }

            _chat.DispatchGlobalAnnouncement(authorizationMessage, colorOverride: proto.AnnouncementColor);
        }

        // Per-request announcement (ERT dispatch notice, Code GAMMA text, etc.)
        if (proto.Announcement != null)
            _chat.DispatchGlobalAnnouncement(
                Loc.GetString(proto.Announcement),
                colorOverride: proto.AnnouncementColor);

    }

    /// <summary>
    /// Refund the fee that was held at request time due to it being denied, cancelled, etc.
    /// </summary>
    private void RefundFee(SecureTerminalProposalData proposal, float fraction = 1f)
    {
        if (_protos.TryIndex<SecureCommandTerminalRequestPrototype>(proposal.RequestId, out var proto))
            RefundFee(proposal.Requester, proto, fraction);
    }

    private void CancelProposal(EntityUid stationUid, SecureCommandTerminalStationComponent stationComp,
        string requestId, SecureTerminalProposalData proposal, EntityUid terminalUid, EntityUid actor, bool admin,
        bool veto = false)
    {
        CloseAdminApprovalEuis(stationUid, requestId);
        stationComp.ActiveProposals.Remove(requestId);
        RefundFee(proposal);

        var actorName = "authorization presence";
        if (actor.IsValid())
        {
            var name = MetaData(actor).EntityName;
            var job = GetJobName(actor);
            if (_idCard.TryFindIdCard(actor, out var idCard))
            {
                name = idCard.Comp.FullName ?? name;
                job = idCard.Comp.LocalizedJobTitle ?? job;
            }

            actorName = $"{name} ({job})";
        }
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{actorName} cancelled secure terminal proposal: {requestId}");

        if (_protos.TryIndex<SecureCommandTerminalRequestPrototype>(requestId, out var proto))
        {
            var vetoers = string.Join(", ", proposal.Vetoers
                .GroupBy(vetoer => vetoer.PlayerUid)
                .Select(group => group.First())
                .Select(vetoer => $"{vetoer.Name} ({vetoer.Job})"));
            _chatManager.SendAdminAnnouncement(veto
                ? Loc.GetString("secure-terminal-proposal-vetoed-by",
                    ("vetoers", vetoers),
                    ("request", Loc.GetString(proto.Name)))
                : Loc.GetString("secure-terminal-proposal-cancelled-by",
                    ("actor", actorName),
                    ("request", Loc.GetString(proto.Name))));

            if (proto.ProposalAnnouncement)
            {
                if (veto)
                {
                    _chat.DispatchGlobalAnnouncement(
                        Loc.GetString("secure-terminal-proposal-vetoed-by",
                            ("vetoers", vetoers),
                            ("request", Loc.GetString(proto.Name))),
                        colorOverride: proto.AnnouncementColor);
                }
                else
                {
                    var locKey = admin
                        ? "secure-terminal-proposal-denied-cc"
                        : "secure-terminal-proposal-denied";
                    _chat.DispatchGlobalAnnouncement(
                        Loc.GetString(locKey, ("request", Loc.GetString(proto.Name))),
                        colorOverride: proto.AnnouncementColor);
                }
            }

            if (terminalUid.IsValid() && !admin)
                _radio.SendRadioMessage(terminalUid,
                    Loc.GetString("secure-terminal-radio-denied",
                        ("request", Loc.GetString(proto.Name))),
                    "Command", terminalUid);
        }

        UpdateAllConsolesForStation(stationUid);
    }

    private void RefundFee(EntityUid requester, SecureCommandTerminalRequestPrototype proto, float fraction = 1f)
    {
        if (proto.Fee <= 0 || !requester.IsValid())
            return;

        var amount = (int)(proto.Fee * fraction);
        if (amount > 0)
        {
            _playerResources.TryUpdateResource(requester, "credits", amount);
            _roundStatistics.RecordSecureTerminalRefund(proto.ID, proto.ActionType, amount);
        }
    }

    /// <summary>Execute the prototype's configured action against the station.</summary>
    private void ExecuteAction(EntityUid stationUid, SecureCommandTerminalRequestPrototype proto)
    {
        switch (proto.ActionType)
        {
            case SecureTerminalActionType.GameRule:
                if (proto.GameruleId != null)
                    _gameTicker.StartGameRule(proto.GameruleId);
                break;

            case SecureTerminalActionType.AlertLevel:
                if (proto.AlertLevel != null)
                    _alertLevel.SetLevel(stationUid, proto.AlertLevel, true, true, true, false);
                break;

            case SecureTerminalActionType.Armory:
                if (proto.ArmoryKey != null)
                    _armory.SendArmory(stationUid, proto.ArmoryKey);
                break;

            case SecureTerminalActionType.NukeCodes:
                _nukeCodeSystem.SendNukeCodes(stationUid);
                break;

            case SecureTerminalActionType.AirlockAccess:
                var airlockQuery = AllEntityQuery<AirlockComponent, TransformComponent>();
                while (airlockQuery.MoveNext(out var ent, out var airlockcomp, out var xform))
                {
                    if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != stationUid)
                        continue;

                    if (HasComp<FirelockComponent>(ent))
                        continue;

                    if (proto.AllowedAccesses is null || proto.AllowedAccesses.Count() <= 0)
                        _airlock.SetEmergencyAccess((ent, airlockcomp), proto.AccessEnabled);
                    else
                        if (_access.GetMainAccessReader(ent, out var accessEnt) && _access.AreAccessTagsAllowed(proto.AllowedAccesses, accessEnt.Value.Comp))
                            _airlock.SetEmergencyAccess((ent, airlockcomp), proto.AccessEnabled);
                }
                break;

            case SecureTerminalActionType.EscapePods:
                var escapePodConsole = AllEntityQuery<PodConsoleComponent, TransformComponent>();
                while (escapePodConsole.MoveNext(out var ent,out var podConsole, out var xform))
                {
                    podConsole.Locked = false;
                    Dirty(ent, podConsole);
                }

                break;

            case SecureTerminalActionType.Announcement:
                // Nothing to execute — the announcement is dispatched on full authorization.
                break;
        }
    }

    private static List<bool> BuildSatisfiedGroups(SecureTerminalProposalData proposal, int schemeIndex, int groupCount)
    {
        var result = new List<bool>(new bool[groupCount]);
        foreach (var (_, _, _, _, authorizerSchemeIndex, groupIndex) in proposal.Authorizers)
            if (authorizerSchemeIndex == schemeIndex && groupIndex >= 0 && groupIndex < result.Count)
                result[groupIndex] = true;
        return result;
    }

    private void RemoveAbsentVetoers(SecureTerminalProposalData proposal)
    {
        var removedTerminals = new List<EntityUid>();
        proposal.Vetoers.RemoveAll(vetoer =>
        {
            if (_ui.IsUiOpen(vetoer.TerminalUid, SecureCommandTerminalUiKey.Key, vetoer.PlayerUid))
                return false;

            removedTerminals.Add(vetoer.TerminalUid);
            return true;
        });

        removedTerminals.ForEach(terminalUid => proposal.UsedVetoTerminals.Remove(terminalUid));
    }

    private bool HasLostVetoPresence(SecureTerminalProposalData proposal) =>
        proposal.Vetoers.Any(vetoer =>
            !_ui.IsUiOpen(vetoer.TerminalUid, SecureCommandTerminalUiKey.Key, vetoer.PlayerUid));

    private bool TryVeto(EntityUid actor, SecureTerminalProposalData proposal,
        SecureCommandTerminalRequestPrototype proto, EntityUid terminalUid)
    {
        var accessTags = _idCard.TryFindIdCard(actor, out var idCard)
            ? _access.FindAccessTags(idCard.Owner)
            : Array.Empty<ProtoId<Content.Shared.Access.AccessLevelPrototype>>();
        var vetoed = false;
        for (var schemeIndex = 0; schemeIndex < proto.VetoSchemes.Count; schemeIndex++)
        {
            if (proposal.Vetoers.Any(v => v.PlayerUid == actor && v.SchemeIndex == schemeIndex))
                continue;

            var scheme = proto.VetoSchemes[schemeIndex];
            var satisfied = BuildSatisfiedVetoGroups(proposal, schemeIndex, scheme.Groups.Count);
            for (var groupIndex = 0; groupIndex < scheme.Groups.Count; groupIndex++)
            {
                if (satisfied[groupIndex] || !scheme.Groups[groupIndex].Any(tag => accessTags.Contains(tag)))
                    continue;

                string name, job;
                if (_idCard.TryFindIdCard(actor, out var vetoIdCard))
                {
                    name = vetoIdCard.Comp.FullName ?? MetaData(actor).EntityName;
                    job = vetoIdCard.Comp.LocalizedJobTitle ?? GetJobName(actor);
                }
                else
                {
                    name = MetaData(actor).EntityName;
                    job = GetJobName(actor);
                }
                proposal.UsedVetoTerminals.Add(terminalUid);
                proposal.Vetoers.Add((actor, name, job, terminalUid, schemeIndex, groupIndex));
                vetoed = true;
                break;
            }
        }

        return vetoed;
    }

    private static bool HasVeto(SecureTerminalProposalData proposal, SecureCommandTerminalRequestPrototype proto) =>
        proto.VetoSchemes.Select((scheme, index) => BuildSatisfiedVetoGroups(proposal, index, scheme.Groups.Count))
            .Any(groups => groups.All(satisfied => satisfied));

    private static List<bool> BuildSatisfiedVetoGroups(SecureTerminalProposalData proposal, int schemeIndex, int groupCount)
    {
        var result = new List<bool>(new bool[groupCount]);
        foreach (var (_, _, _, _, vetoSchemeIndex, groupIndex) in proposal.Vetoers)
            if (vetoSchemeIndex == schemeIndex && groupIndex >= 0 && groupIndex < result.Count)
                result[groupIndex] = true;
        return result;
    }

    private void RemoveAbsentAuthorizers(SecureTerminalProposalData proposal)
    {
        if (proposal.Requester.IsValid() &&
            !_ui.IsUiOpen(proposal.RequesterTerminal, SecureCommandTerminalUiKey.Key, proposal.Requester))
        {
            proposal.Authorizers.RemoveAll(authorizer => authorizer.PlayerUid == proposal.Requester);
        }

        var removedTerminals = new List<EntityUid>();
        proposal.Authorizers.RemoveAll(authorizer =>
        {
            if (_ui.IsUiOpen(authorizer.TerminalUid, SecureCommandTerminalUiKey.Key, authorizer.PlayerUid))
                return false;

            removedTerminals.Add(authorizer.TerminalUid);
            return true;
        });

        removedTerminals.ForEach(terminalUid => proposal.UsedTerminals.Remove(terminalUid));
    }

    private bool IsRequesterPresent(SecureTerminalProposalData proposal) =>
        proposal.Requester.IsValid() &&
        _ui.IsUiOpen(proposal.RequesterTerminal, SecureCommandTerminalUiKey.Key, proposal.Requester);

    private bool HasLostAuthorizationPresence(SecureTerminalProposalData proposal) =>
        proposal.Authorizers.Any(authorizer =>
            !_ui.IsUiOpen(authorizer.TerminalUid, SecureCommandTerminalUiKey.Key, authorizer.PlayerUid));

    private bool CanRequest(EntityUid actor, SecureCommandTerminalRequestPrototype proto)
    {
        var tags = _access.FindAccessTags(actor);
        return proto.AuthSchemes.Any(scheme => scheme.Groups.Any(group =>
            group.Any(tag => tags.Contains(tag))));
    }

    private bool IsWarDeclared()
    {
        var query = EntityQueryEnumerator<NukeopsRuleComponent>();
        while (query.MoveNext(out _, out var nukeops))
            if (nukeops.WarDeclaredTime != null) return true;
        return false;
    }

    private string GetJobName(EntityUid actor)
    {
        if (_mind.TryGetMind(actor, out var mindUid, out _)
            && _jobs.MindTryGetJobName(mindUid, out var jobName)
            && jobName != null)
            return jobName;
        return Loc.GetString("secure-terminal-unknown-job");
    }

    private void UpdateConsoleInterface(EntityUid consoleUid)
    {
        var stationUid = _stations.GetOwningStation(consoleUid);

        var proposals = new List<SecureTerminalProposalState>();
        var coolingDown = new Dictionary<string, TimeSpan>();
        var usedOnce = new HashSet<string>();
        string? currentAlertLevel = null;
        var alertLevelSetAt = TimeSpan.Zero;
        SecureCommandTerminalStationComponent? stationComp = null;

        if (stationUid != null && TryComp(stationUid.Value, out stationComp))
        {
            coolingDown = new Dictionary<string, TimeSpan>(stationComp.Cooldowns);
            usedOnce = stationComp.UsedOnce;
            alertLevelSetAt = stationComp.AlertLevelSetAt;

            if (TryComp<AlertLevelComponent>(stationUid.Value, out var alertComp))
                currentAlertLevel = alertComp.CurrentLevel;

            foreach (var (requestId, data) in stationComp.ActiveProposals)
            {
                if (!_protos.TryIndex<SecureCommandTerminalRequestPrototype>(requestId, out var proto))
                    continue;

                var schemeStates = proto.AuthSchemes.Select((scheme, schemeIndex) =>
                {
                    var groups = scheme.Groups;
                    var satisfiedGroups = BuildSatisfiedGroups(data, schemeIndex, groups.Count);
                    var authByGroup = data.Authorizers
                        .Where(a => a.SchemeIndex == schemeIndex)
                        .ToDictionary(a => a.GroupIndex, a => (a.Name, a.Job));
                    var authorizedBy = Enumerable.Range(0, groups.Count)
                        .Select(i => authByGroup.TryGetValue(i, out var auth) ? auth : (string.Empty, string.Empty))
                        .ToList();

                    return new SecureTerminalAuthSchemeState
                    {
                        Id = scheme.Id,
                        Name = scheme.Name,
                        Description = scheme.Description,
                        AuthorizedBy = authorizedBy,
                        GroupsSatisfied = satisfiedGroups,
                        GroupLabels = groups.Select(g => string.Join(" / ", g)).ToList(),
                    };
                }).ToList();

                var vetoSchemeStates = proto.VetoSchemes.Select((scheme, schemeIndex) =>
                {
                    var groups = scheme.Groups;
                    var satisfiedGroups = BuildSatisfiedVetoGroups(data, schemeIndex, groups.Count);
                    var vetoByGroup = data.Vetoers
                        .Where(v => v.SchemeIndex == schemeIndex)
                        .ToDictionary(v => v.GroupIndex, v => (v.Name, v.Job));
                    var authorizedBy = Enumerable.Range(0, groups.Count)
                        .Select(i => vetoByGroup.TryGetValue(i, out var veto) ? veto : (string.Empty, string.Empty))
                        .ToList();

                    return new SecureTerminalAuthSchemeState
                    {
                        Id = scheme.Id,
                        Name = scheme.Name,
                        Description = scheme.Description,
                        AuthorizedBy = authorizedBy,
                        GroupsSatisfied = satisfiedGroups,
                        GroupLabels = groups.Select(g => string.Join(" / ", g)).ToList(),
                    };
                }).ToList();

                proposals.Add(new SecureTerminalProposalState
                {
                    RequestId = requestId,
                    AuthSchemes = schemeStates,
                    VetoSchemes = vetoSchemeStates,
                    AuthorizedBy = data.Authorizers
                        .GroupBy(authorizer => authorizer.PlayerUid)
                        .Select(group => group.First())
                        .Select(authorizer => (authorizer.Name, authorizer.Job))
                        .ToList(),
                    ActivateAt = data.ActivateAt,
                    Status = data.Status,
                });
            }
        }

        _ui.SetUiState(consoleUid, SecureCommandTerminalUiKey.Key,
            new SecureCommandTerminalInterfaceState(proposals, IsWarDeclared(), coolingDown, currentAlertLevel, usedOnce, alertLevelSetAt,
                stationComp != null ? new Dictionary<string, TimeSpan>(stationComp.DeployedArmories) : new Dictionary<string, TimeSpan>()));
    }

    private void UpdateAllConsolesForStation(EntityUid stationUid)
    {
        var query = EntityQueryEnumerator<SecureCommandTerminalConsoleComponent>();
        while (query.MoveNext(out var consoleUid, out var comp))
            if (_stations.GetOwningStation(consoleUid) == stationUid)
                UpdateConsoleInterface(consoleUid);
    }
}
