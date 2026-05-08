using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Server.Starlight.AlertArmory;
using Content.Shared.Access.Systems;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Content.Shared.Starlight.SecureTerminal;
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
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Content.Server.Nuke;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Toggleable;
using Content.Server._Starlight.Administration.Systems;
using Content.Shared._NullLink;

namespace Content.Server.Starlight.SecureTerminal;

/// <summary>
/// Drives the Secure Command Terminal — proposal creation, multi-party authorization,
/// countdown timers, fee deduction, salary penalties, and final action execution.
/// </summary>
public sealed class SecureCommandTerminalSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly StationSystem _stations = default!;
    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly AlertArmorySystem _armory = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly NukeCodePaperSystem _nukeCodeSystem = default!;
    [Dependency] private readonly SharedAirlockSystem _airlock = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly AutoDiscordLogSystem _autolog = default!;
    [Dependency] private readonly ISharedNullLinkPlayerResourcesManager _playerResources = default!;

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

            // Fire activating proposals whose timer has elapsed
            // And check AuthTimer.
            List<string>? toFire = null;
            List<string>? toExpire = null;
            List<EntityUid>? authToLightUp = null;
            foreach (var (requestId, proposal) in stationComp.ActiveProposals)
            {
                if (proposal.Status == SecureTerminalProposalStatus.Pending)
                {
                    if (proposal.AuthTimer.HasValue && proposal.AuthTimer.Value <= now)
                        (toExpire ??= new()).Add(requestId);

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

            if (toExpire != null)
                foreach (var requestId in toExpire)
                    stationComp.ActiveProposals.Remove(requestId);

            if (toFire != null)
                foreach (var requestId in toFire)
                {
                    if (_protos.TryIndex<SecureCommandTerminalRequestPrototype>(requestId, out var proto))
                    {
                        ExecuteAction(stationUid, proto);
                        stationComp.ActiveProposals.Remove(requestId);
                        if (proto.ActionType == SecureTerminalActionType.Armory)
                        {
                            // Track as deployed so it can still be recalled
                            var authorizedAt = now - TimeSpan.FromSeconds(proto.ActivationDelaySecs);
                            stationComp.DeployedArmories[requestId] = authorizedAt;
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

            if (toExpire is null && toFire is null) continue;

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

        // We check and charge the fee now, deny on sufficient funds.
        if (proto.Fee > 0)
            if (_playerResources.TryGetResource(actor, "credits", out var balance) && balance < proto.Fee)
            {
                _popup.PopupCursor($"Insufficient funds. Required: {proto.Fee}\u20a1", actor, PopupType.Medium);
                return;
            }
            else
            {
                _playerResources.TryUpdateResource(actor, "credits", -proto.Fee);
                _popup.PopupCursor($"Debited {proto.Fee}\u20a1. Balance: {balance -= proto.Fee}\u20a1", actor, PopupType.Medium);
            }

        // Create the proposal
        var proposal = new SecureTerminalProposalData { RequestId = msg.RequestId };
        stationComp.ActiveProposals[msg.RequestId] = proposal;

        if (reason is not null)
            proposal.Reason = reason;

        // The requestor automatically authorizes their matching group(s)
        TryAuthorize(actor, proposal, proto, uid, comp);

        if (proto.AuthTimer > 0)
            proposal.AuthTimer = _timing.CurTime + TimeSpan.FromSeconds(proto.AuthTimer);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(actor):player} created secure terminal proposal: {msg.RequestId}");

        _autolog.LogToDiscord($"created secure terminal proposal: {msg.RequestId}", ToPrettyString(actor));

        _chatManager.SendAdminAnnouncement(
            $"Secure Terminal — {MetaData(actor).EntityName} ({GetJobName(actor)}) proposed: {Loc.GetString(proto.Name)}.");

        var proposalAnnounce = Loc.GetString("secure-terminal-proposal-created", ("request", Loc.GetString(proto.Name)));
        if (reason is not null)
            proposalAnnounce = Loc.GetString("secure-terminal-proposal-created-reason", ("request", Loc.GetString(proto.Name)), ("reason", reason));

        if (proto.ProposalAnnouncement)
            _chat.DispatchGlobalAnnouncement(proposalAnnounce, colorOverride: proto.AnnouncementColor);

        var proposalRadio = Loc.GetString("secure-terminal-radio-proposal", ("request", Loc.GetString(proto.Name)));
        if (reason is not null)
            proposalRadio = Loc.GetString("secure-terminal-radio-proposal-reason", ("request", Loc.GetString(proto.Name)), ("reason", reason));

        _radio.SendRadioMessage(uid, proposalRadio, "Command", uid);

        CheckAndStartCountdown(stationUid, stationComp, msg.RequestId, proto);
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
            _autolog.LogToDiscord($"authorized secure terminal proposal: {msg.RequestId}", ToPrettyString(actor));
        }
        else if (proposal.Authorizers.Any(a => a.PlayerUid == actor))
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-already-authorized"), actor, PopupType.Medium);
            return;
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

        if (!stationComp.ActiveProposals.ContainsKey(msg.RequestId))
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-no-active-proposal"), actor, PopupType.Medium);
            return;
        }

        // Any Command member can deny
        var tags = _access.FindAccessTags(actor);
        if (!tags.Contains((Robust.Shared.Prototypes.ProtoId<Content.Shared.Access.AccessLevelPrototype>)"Command"))
        {
            _popup.PopupCursor(Loc.GetString("secure-terminal-request-denied"), actor, PopupType.Medium);
            return;
        }

        stationComp.ActiveProposals.Remove(msg.RequestId);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(actor):player} denied secure terminal proposal: {msg.RequestId}");

        if (_protos.TryIndex<SecureCommandTerminalRequestPrototype>(msg.RequestId, out var proto))
        {
            _chatManager.SendAdminAnnouncement(
                $"Secure Terminal — {MetaData(actor).EntityName} ({GetJobName(actor)}) DENIED / cancelled: {Loc.GetString(proto.Name)}.");

            if (proto.ProposalAnnouncement)
            {
                var locKey = comp.Admin
                    ? "secure-terminal-proposal-denied-cc"
                   : "secure-terminal-proposal-denied";
                _chat.DispatchGlobalAnnouncement(
                    Loc.GetString(locKey, ("request", Loc.GetString(proto.Name))),
                    colorOverride: proto.AnnouncementColor);
            }

            if (!comp.Admin)
                _radio.SendRadioMessage(uid,
                    Loc.GetString("secure-terminal-radio-denied",
                        ("request", Loc.GetString(proto.Name))),
                    "Command", uid);
        }

        UpdateAllConsolesForStation(stationUid.Value);
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

        stationComp.ActiveProposals.Remove(msg.RequestId);
        stationComp.DeployedArmories.Remove(msg.RequestId);
        stationComp.UsedOnce.Add(msg.RequestId);

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

    /// <summary>
    /// Attempts to satisfy the first available auth group for <paramref name="actor"/>.
    /// Returns true if an unsatisfied group was claimed.
    /// </summary>
    private bool TryAuthorize(EntityUid actor, SecureTerminalProposalData proposal,
        SecureCommandTerminalRequestPrototype proto, EntityUid terminalUid, SecureCommandTerminalConsoleComponent terminal)
    {
        if (terminal.Admin)
            return true;

        var accessTags = _access.FindAccessTags(actor);
        var satisfied = BuildSatisfiedGroups(proposal, proto);

        for (var i = 0; i < proto.AuthGroups.Count; i++)
        {
            if (satisfied[i]) continue;
            var group = proto.AuthGroups[i];
            if (group.Any(tag => accessTags.Contains((Robust.Shared.Prototypes.ProtoId<Content.Shared.Access.AccessLevelPrototype>)tag)))
            {
                string name, job;
                if (_idCard.TryFindIdCard(actor, out var idCard))
                {
                    name = idCard.Comp.FullName ?? MetaData(actor).EntityName;
                    job = idCard.Comp.LocalizedJobTitle ?? GetJobName(actor);
                }
                else
                {
                    name = MetaData(actor).EntityName;
                    job = GetJobName(actor);
                }
                proposal.UsedTerminals.Add(terminalUid);
                proposal.Authorizers.Add((actor, name, job, i));
                return true;
            }
        }
        return false;
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

        var satisfied = BuildSatisfiedGroups(proposal, proto);
        if (!satisfied.All(s => s)) return;

        // Admin Approval
        if (proto.RequiresAdminApproval && !proposal.AdminApproved)
        {
            if (_adminManager.ActiveAdmins.Count() > 0 || !proto.BypassIfNoAdmin)
            {
                proposal.AuthTimer = null;
                _chat.DispatchGlobalAnnouncement(Loc.GetString("secure-terminal-awaiting-admin", ("request", Loc.GetString(proto.Name))), colorOverride: proto.AnnouncementColor);
                _chatManager.SendAdminAlert(Loc.GetString("secure-terminal-admin", ("request", Loc.GetString(proto.Name)), ("reason", proposal.Reason)));
                _audio.PlayGlobal("/Audio/Misc/adminlarm.ogg",
                    Filter.Empty().AddPlayers(_adminManager.ActiveAdmins),
                    false,
                    AudioParams.Default.WithVolume(-8f));
                return;
            }
        }

        _autolog.LogToDiscord($"activating secure terminal proposal: {requestId}");

        proposal.Status = SecureTerminalProposalStatus.Activating;
        proposal.ActivateAt = _timing.CurTime + TimeSpan.FromSeconds(proto.ActivationDelaySecs);

        if (proto.ProposalAnnouncement)
        {
            // Authorized-by announcement listing all signatories
            var signatories = string.Join(", ",
                proposal.Authorizers.Select(a => $"{a.Name} ({a.Job})"));
            _chat.DispatchGlobalAnnouncement(
                Loc.GetString("secure-terminal-authorized-by",
                    ("request", Loc.GetString(proto.Name)),
                    ("signatories", signatories)),
                colorOverride: proto.AnnouncementColor);
        }

        // Per-request announcement (ERT dispatch notice, Code GAMMA text, etc.)
        if (proto.Announcement != null)
            _chat.DispatchGlobalAnnouncement(
                Loc.GetString(proto.Announcement),
                colorOverride: proto.AnnouncementColor);

        // Apply salary penalty to station
        stationComp.SalaryPenalty = Math.Min(0.8f, stationComp.SalaryPenalty + proto.SalaryPenalty);
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
        }
    }

    private static List<bool> BuildSatisfiedGroups(SecureTerminalProposalData proposal,
        SecureCommandTerminalRequestPrototype proto)
    {
        var result = new List<bool>(new bool[proto.AuthGroups.Count]);
        foreach (var (_, _, _, groupIdx) in proposal.Authorizers)
            if (groupIdx >= 0 && groupIdx < result.Count)
                result[groupIdx] = true;
        return result;
    }

    private bool CanRequest(EntityUid actor, SecureCommandTerminalRequestPrototype proto)
    {
        var tags = _access.FindAccessTags(actor);
        return proto.AuthGroups.Any(group =>
            group.Any(tag => tags.Contains((Robust.Shared.Prototypes.ProtoId<Content.Shared.Access.AccessLevelPrototype>)tag)));
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

                var satisfiedGroups = BuildSatisfiedGroups(data, proto);
                var labels = proto.AuthGroupLabels.Count == proto.AuthGroups.Count
                    ? proto.AuthGroupLabels
                    : proto.AuthGroups.Select(g => string.Join(" / ", g)).ToList();

                // Build AuthorizedBy indexed by group so the client can show who authorized each slot
                var authByGroup = data.Authorizers.ToDictionary(a => a.GroupIndex, a => (a.Name, a.Job));
                var authorizedBy = Enumerable.Range(0, proto.AuthGroups.Count)
                    .Select(i => authByGroup.TryGetValue(i, out var auth) ? auth : (string.Empty, string.Empty))
                    .ToList();

                proposals.Add(new SecureTerminalProposalState
                {
                    RequestId = requestId,
                    AuthorizedBy = authorizedBy,
                    GroupsSatisfied = satisfiedGroups,
                    GroupLabels = labels,
                    ActivateAt = data.ActivateAt,
                    AuthTimer = data.AuthTimer,
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
