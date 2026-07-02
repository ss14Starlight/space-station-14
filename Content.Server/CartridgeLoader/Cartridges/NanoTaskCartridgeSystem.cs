using Content.Shared.GameTicking; // Starlight - Tidr: round-end escrow refund
using Content.Server.Station.Systems; // Starlight - Tidr
using Content.Server._Starlight.Tidr;  // Starlight - Tidr
using Content.Shared.Access.Components; // Starlight - Tidr
using Content.Shared.Administration.Logs; // Starlight - Tidr: money audit trail
using Content.Shared.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Content.Shared.Database; // Starlight - Tidr
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.PDA; // Starlight - Tidr
using Content.Shared._NullLink; // Starlight - Tidr: player credit resources
using Robust.Server.Player; // Starlight - Tidr
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network; // Starlight - Tidr
using Robust.Shared.Player; // Starlight - Tidr
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared.Abilities.Mime; // Starlight
using Content.Server.Popups; // Starlight

namespace Content.Server.CartridgeLoader.Cartridges;

/// <summary>
///     Server-side class implementing the core UI logic of Tidr (the NanoTask rework):
///     a station-wide job board with credit escrow. Cards gate permissions; player
///     accounts carry the money.
/// </summary>
public sealed partial class NanoTaskCartridgeSystem : SharedNanoTaskCartridgeSystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PopupSystem _popupSystem = default!; // Starlight
    [Dependency] private StationSystem _stationSystem = default!; // Starlight - Tidr
    [Dependency] private ISharedNullLinkPlayerResourcesManager _playerResources = default!; // Starlight - Tidr
    [Dependency] private IPlayerManager _players = default!; // Starlight - Tidr
    [Dependency] private ISharedAdminLogManager _adminLogger = default!; // Starlight - Tidr

    /// <summary>
    ///     Starlight - Tidr: NanoTrasen's cut of every completed bounty. Floor'd, so tiny
    ///     rewards round in the Tider's favour.
    /// </summary>
    private const float NTCut = 0.05f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NanoTaskCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<NanoTaskCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);

        SubscribeLocalEvent<NanoTaskCartridgeComponent, CartridgeRemovedEvent>(OnCartridgeRemoved);

        SubscribeLocalEvent<NanoTaskInteractionComponent, InteractUsingEvent>(OnInteractUsing);

        // Starlight - Tidr: refund all outstanding escrow before the round tears down;
        // credits live on persistent player accounts, so unrefunded escrow is destroyed money.
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        var query = EntityQueryEnumerator<TidrBoardComponent>();
        while (query.MoveNext(out _, out var board))
        {
            foreach (var (taskId, amount) in board.Escrow)
            {
                if (board.OwnerUsers.TryGetValue(taskId, out var user) && TryCredit(user, amount))
                    _adminLogger.Add(
                        LogType.Action,
                        LogImpact.Medium,
                        $"Round end: refunded {amount} cr. of Tidr escrow for task {taskId}");
            }
            board.Escrow.Clear();
        }
    }

    private void OnCartridgeRemoved(Entity<NanoTaskCartridgeComponent> ent, ref CartridgeRemovedEvent args)
    {
        if (!_cartridgeLoader.HasProgram<NanoTaskCartridgeComponent>(args.Loader))
        {
            RemComp<NanoTaskInteractionComponent>(args.Loader);
        }
    }

    private void OnInteractUsing(Entity<NanoTaskInteractionComponent> ent, ref InteractUsingEvent args)
    {
        if (!_cartridgeLoader.TryGetProgram<NanoTaskCartridgeComponent>(ent.Owner, out var uid, out var program))
        {
            return;
        }
        if (!TryComp<NanoTaskPrintedComponent>(args.Used, out var printed))
        {
            return;
        }
        if (printed.Task is NanoTaskItem item)
        {
            // Starlight - Tidr: scanning a printed task re-posts it under the scanner's own
            // ID, unclaimed, with the reward zeroed. Paper can't carry funded escrow: honouring
            // a printed reward would either mint unfunded bounties or drain the scanner blind.
            if (TryGetBoard(ent.Owner, out var scanBoard))
            {
                if (!TryGetInsertedId(ent.Owner, out var scanCard))
                {
                    _popupSystem.PopupEntity(Loc.GetString("tidr-no-id"), args.User, args.User);
                    return;
                }
                var scanName = GetCardName(scanCard);
                var reposted = new NanoTaskItem(item.Description, scanName, false, item.Priority, item.Location, 0, null);
                var newId = scanBoard.Counter++;
                scanBoard.Tasks.Add(new(newId, reposted));
                scanBoard.Owners[newId] = scanCard;
                if (_players.TryGetSessionByEntity(args.User, out var scanSession))
                    scanBoard.OwnerUsers[newId] = scanSession.UserId;
            }
            else
                program.Tasks.Add(new(program.Counter++, printed.Task));
            args.Handled = true;
            Del(args.Used);
            if (_stationSystem.GetOwningStation(ent.Owner) is { } scanStation)
                RefreshStation(scanStation);
            else
                UpdateUiState(new Entity<NanoTaskCartridgeComponent>(uid.Value, program), ent.Owner);
        }
    }

    /// <summary>
    /// This gets called when the ui fragment needs to be updated for the first time after activating
    /// </summary>
    private void OnUiReady(Entity<NanoTaskCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUiState(ent, args.Loader);
    }

    private void SetupPrintedTask(EntityUid uid, NanoTaskItem item)
    {
        PaperComponent? paper = null;
        NanoTaskPrintedComponent? printed = null;
        if (!Resolve(uid, ref paper, ref printed))
            return;

        printed.Task = item;
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("nano-task-printed-description", ("description", FormattedMessage.EscapeText(item.Description))));
        msg.PushNewline();
        msg.AddMarkupOrThrow(Loc.GetString("nano-task-printed-requester", ("requester", FormattedMessage.EscapeText(item.TaskIsFor))));
        msg.PushNewline();
        // Starlight - Tidr: the physical ticket carries the meet point and the pay
        if (!string.IsNullOrWhiteSpace(item.Location))
        {
            msg.AddMarkupOrThrow(Loc.GetString("tidr-printed-location", ("location", FormattedMessage.EscapeText(item.Location))));
            msg.PushNewline();
        }
        if (item.Reward > 0)
        {
            msg.AddMarkupOrThrow(Loc.GetString("tidr-printed-reward", ("amount", item.Reward)));
            msg.PushNewline();
        }
        msg.AddMarkupOrThrow(item.Priority switch {
            NanoTaskPriority.High => Loc.GetString("nano-task-printed-high-priority"),
            NanoTaskPriority.Medium => Loc.GetString("nano-task-printed-medium-priority"),
            NanoTaskPriority.Low => Loc.GetString("nano-task-printed-low-priority"),
            _ => "",
        });

        _paper.SetContent((uid, paper), msg.ToMarkup());
    }

    /// <summary>
    /// The ui messages received here get wrapped by a CartridgeMessageEvent and are relayed from the <see cref="CartridgeLoaderSystem"/>
    /// </summary>
    /// <remarks>
    /// The cartridge specific ui message event needs to inherit from the CartridgeMessageEvent
    /// </remarks>
    private void OnUiMessage(Entity<NanoTaskCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        if (args is not NanoTaskUiMessageEvent message)
            return;

        var loader = GetEntity(args.LoaderUid); // Starlight - Tidr
        var station = _stationSystem.GetOwningStation(loader); // Starlight - Tidr

        switch (message.Payload)
        {
            case NanoTaskAddTask task:
            {
                if (!task.Item.Validate())
                    return;
                // Starlight - Tidr: posting requires an ID card in the PDA
                if (!TryGetInsertedId(loader, out var posterCard))
                {
                    _popupSystem.PopupEntity(Loc.GetString("tidr-no-id"), args.Actor, args.Actor);
                    return;
                }
                if (!TryGetBoard(loader, out var addBoard))
                {
                    // off-station: plain notepad behaviour, no board, no escrow
                    ent.Comp.Tasks.Add(new(ent.Comp.Counter++, task.Item));
                    break;
                }

                var reward = Math.Max(task.Item.Reward, 0);
                NetUserId? posterUser = null;

                // Starlight - Tidr: escrow. The reward leaves the poster's account the moment
                // the task goes up, so the money is guaranteed to exist when the job is done.
                if (reward > 0)
                {
                    if (!_players.TryGetSessionByEntity(args.Actor, out var posterSession))
                    {
                        _popupSystem.PopupEntity(Loc.GetString("tidr-no-account"), args.Actor, args.Actor);
                        return;
                    }
                    if (!_playerResources.TryGetResource(args.Actor, "credits", out var balance) || balance < reward)
                    {
                        _popupSystem.PopupEntity(Loc.GetString("tidr-insufficient-funds", ("amount", reward)), args.Actor, args.Actor);
                        return;
                    }
                    _playerResources.TryUpdateResource(args.Actor, "credits", -reward);
                    _adminLogger.Add(
                        LogType.Action,
                        LogImpact.Medium,
                        $"{ToPrettyString(args.Actor):player} escrowed {reward} cr. posting Tidr task \"{task.Item.Description}\"");
                    posterUser = posterSession.UserId;
                }
                else if (_players.TryGetSessionByEntity(args.Actor, out var posterSession))
                    posterUser = posterSession.UserId;

                // name is stamped from the card, never trusted from the client
                var newItem = new NanoTaskItem(task.Item.Description, GetCardName(posterCard), false, task.Item.Priority, task.Item.Location, reward, null);
                var newId = addBoard.Counter++;
                addBoard.Tasks.Add(new(newId, newItem));
                addBoard.Owners[newId] = posterCard;
                if (posterUser is { } pu)
                    addBoard.OwnerUsers[newId] = pu;
                if (reward > 0)
                    addBoard.Escrow[newId] = reward;
                break;
            }
            case NanoTaskUpdateTask task:
            {
                if (!task.Item.Data.Validate())
                    return;
                if (!TryGetBoard(loader, out var updBoard))
                {
                    var lidx = ent.Comp.Tasks.FindIndex(t => t.Id == task.Item.Id);
                    if (lidx != -1)
                        ent.Comp.Tasks[lidx] = task.Item;
                    break;
                }
                // Starlight - Tidr: only the owner card may edit or complete a task
                if (!IsTaskOwner(updBoard, task.Item.Id, loader))
                {
                    _popupSystem.PopupEntity(Loc.GetString("tidr-not-your-task"), args.Actor, args.Actor);
                    return;
                }
                var idx = updBoard.Tasks.FindIndex(t => t.Id == task.Item.Id);
                if (idx == -1)
                    return;
                var existing = updBoard.Tasks[idx];
                var old = existing.Data;
                var incoming = task.Item.Data;

                // Starlight - Tidr: completion is final. Money moves on complete; there is no un-complete.
                if (old.IsTaskDone && !incoming.IsTaskDone)
                {
                    _popupSystem.PopupEntity(Loc.GetString("tidr-already-completed"), args.Actor, args.Actor);
                    return;
                }
                if (old.IsTaskDone)
                    return; // completed tasks are read-only apart from delete

                // Server-controlled fields are rebuilt from server truth, never taken from the
                // client: requester name, reward (escrowed, immutable), and AcceptedBy (a stale
                // edit popup snapshot must not be able to wipe a claim made while it was open).
                var completing = incoming.IsTaskDone;
                var merged = new NanoTaskItem(
                    incoming.Description,
                    old.TaskIsFor,
                    completing,
                    incoming.Priority,
                    incoming.Location,
                    old.Reward,
                    old.AcceptedBy);

                if (completing && old.Reward > 0 && updBoard.Escrow.TryGetValue(existing.Id, out var pot))
                {
                    if (updBoard.AccepterUsers.TryGetValue(existing.Id, out var tider))
                    {
                        // pay the Tider, minus the NanoTrasen cut
                        var cut = (int)Math.Floor(pot * NTCut);
                        var payout = pot - cut;
                        if (!TryCredit(tider, payout))
                        {
                            _popupSystem.PopupEntity(Loc.GetString("tidr-tider-offline"), args.Actor, args.Actor);
                            return; // task stays open; escrow stays held
                        }
                        _popupSystem.PopupEntity(Loc.GetString("tidr-paid-out", ("amount", payout)), args.Actor, args.Actor);
                        _adminLogger.Add(
                            LogType.Action,
                            LogImpact.Medium,
                            $"{ToPrettyString(args.Actor):player} completed Tidr task \"{old.Description}\" - paid {payout} cr. to {old.AcceptedBy ?? "unknown"} ({cut} cr. NT cut)");
                    }
                    else
                    {
                        // completing an unclaimed task: nobody to pay, refund the poster
                        if (updBoard.OwnerUsers.TryGetValue(existing.Id, out var owner))
                            TryCredit(owner, pot);
                        _popupSystem.PopupEntity(Loc.GetString("tidr-refunded", ("amount", pot)), args.Actor, args.Actor);
                        _adminLogger.Add(
                            LogType.Action,
                            LogImpact.Medium,
                            $"{ToPrettyString(args.Actor):player} completed unclaimed Tidr task \"{old.Description}\" - {pot} cr. refunded");
                    }
                    updBoard.Escrow.Remove(existing.Id);
                }

                updBoard.Tasks[idx] = new(existing.Id, merged);
                break;
            }
            case NanoTaskAcceptTask task:
            {
                // Starlight - Tidr: toggle claim. Free -> accept. Taken -> only accepter or owner may release.
                if (!TryGetBoard(loader, out var accBoard))
                    return;
                if (!TryGetInsertedId(loader, out var accCard))
                {
                    _popupSystem.PopupEntity(Loc.GetString("tidr-no-id"), args.Actor, args.Actor);
                    return;
                }
                var accIdx = accBoard.Tasks.FindIndex(t => t.Id == task.Id);
                if (accIdx == -1)
                    return;
                var existing = accBoard.Tasks[accIdx];
                var d = existing.Data;
                if (d.IsTaskDone)
                    return;

                if (string.IsNullOrEmpty(d.AcceptedBy))
                {
                    // free -> claim it
                    accBoard.Tasks[accIdx] = new(existing.Id, new NanoTaskItem(d.Description, d.TaskIsFor, d.IsTaskDone, d.Priority, d.Location, d.Reward, GetCardName(accCard)));
                    accBoard.Accepters[existing.Id] = accCard;
                    if (_players.TryGetSessionByEntity(args.Actor, out var accSession))
                        accBoard.AccepterUsers[existing.Id] = accSession.UserId;
                }
                else
                {
                    // taken -> the accepter or the owner may release it; anyone else is locked out
                    var isAccepter = accBoard.Accepters.TryGetValue(existing.Id, out var acc) && acc == accCard;
                    var isOwner = accBoard.Owners.TryGetValue(existing.Id, out var own) && own == accCard;
                    if (!isAccepter && !isOwner)
                    {
                        _popupSystem.PopupEntity(Loc.GetString("tidr-already-taken"), args.Actor, args.Actor);
                        return;
                    }
                    accBoard.Tasks[accIdx] = new(existing.Id, new NanoTaskItem(d.Description, d.TaskIsFor, d.IsTaskDone, d.Priority, d.Location, d.Reward, null));
                    accBoard.Accepters.Remove(existing.Id);
                    accBoard.AccepterUsers.Remove(existing.Id);
                }
                break;
            }
            case NanoTaskDeleteTask task:
            {
                if (!TryGetBoard(loader, out var delBoard))
                {
                    ent.Comp.Tasks.RemoveAll(t => t.Id == task.Id);
                    break;
                }
                // Starlight - Tidr: only the owner card may delete a task
                if (!IsTaskOwner(delBoard, task.Id, loader))
                {
                    _popupSystem.PopupEntity(Loc.GetString("tidr-not-your-task"), args.Actor, args.Actor);
                    return;
                }
                // deleting an open task refunds its escrow to the poster
                if (delBoard.Escrow.TryGetValue(task.Id, out var refund))
                {
                    if (delBoard.OwnerUsers.TryGetValue(task.Id, out var owner) && TryCredit(owner, refund))
                    {
                        _popupSystem.PopupEntity(Loc.GetString("tidr-refunded", ("amount", refund)), args.Actor, args.Actor);
                        _adminLogger.Add(
                            LogType.Action,
                            LogImpact.Medium,
                            $"{ToPrettyString(args.Actor):player} deleted Tidr task {task.Id} - {refund} cr. refunded to poster");
                    }
                    delBoard.Escrow.Remove(task.Id);
                }
                delBoard.Tasks.RemoveAll(t => t.Id == task.Id);
                delBoard.Owners.Remove(task.Id);
                delBoard.Accepters.Remove(task.Id);
                delBoard.OwnerUsers.Remove(task.Id);
                delBoard.AccepterUsers.Remove(task.Id);
                break;
            }
            case NanoTaskPrintTask task:
            {
                if (!task.Item.Validate())
                    return;
                if (_timing.CurTime < ent.Comp.NextPrintAllowedAfter)
                    return;

                #region Starlight
                // allow mimes to print blank NanoTasks (because it's funny)
                if(TryComp<MimePowersComponent>(args.Actor, out var mime) && !mime.VowBroken)
                {
                    // check that the NanoTask is blank
                    var isBlankNanoTask
                    = string.IsNullOrWhiteSpace(task.Item.Description)
                    && string.IsNullOrWhiteSpace(task.Item.TaskIsFor);

                    // if it's not blank, tell the mime they can't do that while their vow is active
                    if(!isBlankNanoTask)
                    {
                        _popupSystem.PopupEntity(Loc.GetString("mime-cant-speak"), args.Actor, args.Actor);
                        return;
                    }

                }
                #endregion Starlight

                ent.Comp.NextPrintAllowedAfter = _timing.CurTime + ent.Comp.PrintDelay;
                var printed = Spawn("PaperNanoTaskItem", Transform(message.Actor).Coordinates);
                _hands.PickupOrDrop(message.Actor, printed);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/printer.ogg"), ent.Owner);
                SetupPrintedTask(printed, task.Item);
                break;
            }
        }

        // Starlight - Tidr: push the updated board to every Tidr app on the station
        if (station is { } st)
            RefreshStation(st);
        else
            UpdateUiState(ent, loader);
    }

    // ===== Starlight - Tidr: money helpers =====

    /// <summary>
    ///     Credit a player's account by NetUserId. Fails (false) if they're not connected;
    ///     callers decide whether that blocks the action or is best-effort.
    /// </summary>
    private bool TryCredit(NetUserId user, int amount)
    {
        if (amount <= 0)
            return true;
        if (!_players.TryGetSessionById(user, out var session))
            return false;
        return _playerResources.TryUpdateResource(session, "credits", amount);
    }

    /// <summary>
    ///     Resolve the credit balance of whoever is holding this PDA, for the app header.
    ///     -1 if there's no player attached (client hides the readout).
    /// </summary>
    private int GetHolderBalance(EntityUid loader)
    {
        var holder = Transform(loader).ParentUid;
        if (!holder.IsValid())
            return -1;
        if (!_players.TryGetSessionByEntity(holder, out _))
            return -1;
        if (!_playerResources.TryGetResource(holder, "credits", out var balance))
            return -1;
        return (int)balance;
    }

    private string GetCardName(EntityUid card)
    {
        return TryComp<IdCardComponent>(card, out var id) && !string.IsNullOrWhiteSpace(id.FullName)
            ? id.FullName!
            : Loc.GetString("tidr-unknown-poster");
    }

    // ===== Starlight - Tidr: state building =====

    // Build the UI state for one specific PDA, tagging each task with that viewer's
    // ownership / acceptance and including the holder's balance for the header.
    private NanoTaskUiState BuildViewerState(TidrBoardComponent board, EntityUid loader)
    {
        var hasCard = TryGetInsertedId(loader, out var card);
        var entries = new List<NanoTaskViewerEntry>(board.Tasks.Count);
        foreach (var t in board.Tasks)
        {
            var isOwner = hasCard && board.Owners.TryGetValue(t.Id, out var o) && o == card;
            var isAccepter = hasCard && board.Accepters.TryGetValue(t.Id, out var a) && a == card;
            entries.Add(new NanoTaskViewerEntry(t, isOwner, isAccepter));
        }
        return new NanoTaskUiState(entries, GetHolderBalance(loader));
    }

    private void UpdateUiState(Entity<NanoTaskCartridgeComponent> ent, EntityUid loaderUid)
    {
        if (TryGetBoard(loaderUid, out var board))
        {
            _cartridgeLoader.UpdateCartridgeUiState(loaderUid, BuildViewerState(board, loaderUid));
            return;
        }

        var entries = new List<NanoTaskViewerEntry>(ent.Comp.Tasks.Count);
        foreach (var t in ent.Comp.Tasks)
            entries.Add(new NanoTaskViewerEntry(t, true, false)); // off-station notepad: you own everything
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, new NanoTaskUiState(entries, GetHolderBalance(loaderUid)));
    }

    // Push each PDA on the station its own viewer-tagged copy of the board
    private void RefreshStation(EntityUid station)
    {
        var board = EnsureComp<TidrBoardComponent>(station);
        var query = EntityQueryEnumerator<NanoTaskCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out _, out _, out var cart))
        {
            if (cart.LoaderUid is { } loader && _stationSystem.GetOwningStation(loader) == station)
                _cartridgeLoader.UpdateCartridgeUiState(loader, BuildViewerState(board, loader));
        }
    }

    private bool TryGetBoard(EntityUid source, out TidrBoardComponent board)
    {
        board = default!;
        if (_stationSystem.GetOwningStation(source) is not { } station)
            return false;
        board = EnsureComp<TidrBoardComponent>(station);
        return true;
    }

    private bool TryGetInsertedId(EntityUid loader, out EntityUid card)
    {
        card = default;
        if (TryComp<PdaComponent>(loader, out var pda) && pda.ContainedId is { } id)
        {
            card = id;
            return true;
        }
        return false;
    }

    private bool IsTaskOwner(TidrBoardComponent board, int taskId, EntityUid loader)
    {
        return TryGetInsertedId(loader, out var card)
            && board.Owners.TryGetValue(taskId, out var owner)
            && owner == card;
    }
}
