using Content.Server.Station.Systems; // Starlight - Tidr
using Content.Server._Starlight.Tidr;  // Starlight - Tidr
using Content.Shared.Access.Components; // Starlight - Tidr
using Content.Shared.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.PDA; // Starlight - Tidr
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared.Abilities.Mime; // Starlight
using Content.Server.Popups; // Starlight

namespace Content.Server.CartridgeLoader.Cartridges;

/// <summary>
///     Server-side class implementing the core UI logic of NanoTask
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

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NanoTaskCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<NanoTaskCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);

        SubscribeLocalEvent<NanoTaskCartridgeComponent, CartridgeRemovedEvent>(OnCartridgeRemoved);

        SubscribeLocalEvent<NanoTaskInteractionComponent, InteractUsingEvent>(OnInteractUsing);
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
            // Starlight - Tidr: scan a printed task back onto the shared board, stamping the scanner's ID as owner
            if (TryGetBoard(ent.Owner, out var scanBoard))
            {
                var newId = scanBoard.Counter++;
                scanBoard.Tasks.Add(new(newId, printed.Task));
                if (TryGetInsertedId(ent.Owner, out var scanCard))
                    scanBoard.Owners[newId] = scanCard;
            }
            else
                program.Tasks.Add(new(program.Counter++, printed.Task));
            args.Handled = true;
            Del(args.Used);
            // Starlight - Tidr: refresh the whole station if on one, else just this cartridge
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
                if (!task.Item.Validate())
                    return;
                // Starlight - Tidr: a task must be posted under an ID card inserted in the PDA
                if (!TryGetInsertedId(loader, out var posterCard))
                {
                    _popupSystem.PopupEntity(Loc.GetString("tidr-no-id"), args.Actor, args.Actor);
                    return;
                }
                // Starlight - Tidr: stamp the requester name from the poster's ID card, not from client input
                var posterName = TryComp<IdCardComponent>(posterCard, out var idCard) && !string.IsNullOrWhiteSpace(idCard.FullName)
                    ? idCard.FullName!
                    : Loc.GetString("tidr-unknown-poster");
                var newItem = new NanoTaskItem(task.Item.Description, posterName, task.Item.IsTaskDone, task.Item.Priority, task.Item.Location, task.Item.Reward, task.Item.AcceptedBy);
                if (TryGetBoard(loader, out var addBoard))
                {
                    var newId = addBoard.Counter++;
                    addBoard.Tasks.Add(new(newId, newItem));
                    addBoard.Owners[newId] = posterCard; // stamp the poster's card as owner
                }
                else
                    ent.Comp.Tasks.Add(new(ent.Comp.Counter++, newItem));
                break;
            case NanoTaskUpdateTask task:
            {
                if (!task.Item.Data.Validate())
                    return;
                // Starlight - Tidr: only the owner card may edit or complete (Done) a task
                if (TryGetBoard(loader, out var updBoard))
                {
                    if (!IsTaskOwner(updBoard, task.Item.Id, loader))
                    {
                        _popupSystem.PopupEntity(Loc.GetString("tidr-not-your-task"), args.Actor, args.Actor);
                        return;
                    }
                    var idx = updBoard.Tasks.FindIndex(t => t.Id == task.Item.Id);
                    if (idx != -1)
                        updBoard.Tasks[idx] = task.Item;
                }
                else
                {
                    var idx = ent.Comp.Tasks.FindIndex(t => t.Id == task.Item.Id);
                    if (idx != -1)
                        ent.Comp.Tasks[idx] = task.Item;
                }
                break;
            }
            case NanoTaskDeleteTask task:
                // Starlight - Tidr: only the owner card may delete a task
                if (TryGetBoard(loader, out var delBoard))
                {
                    if (!IsTaskOwner(delBoard, task.Id, loader))
                    {
                        _popupSystem.PopupEntity(Loc.GetString("tidr-not-your-task"), args.Actor, args.Actor);
                        return;
                    }
                    delBoard.Tasks.RemoveAll(t => t.Id == task.Id);
                    delBoard.Owners.Remove(task.Id);
                }
                else
                    ent.Comp.Tasks.RemoveAll(t => t.Id == task.Id);
                break;
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

        // Starlight - Tidr: push the updated board to every NanoTask app on the station, not just the sender
        if (station is { } st)
            RefreshStation(st);
        else
            UpdateUiState(ent, loader);
    }

    private void UpdateUiState(Entity<NanoTaskCartridgeComponent> ent, EntityUid loaderUid)
    {
        // Starlight - Tidr: show the shared station board, fall back to the local list off-station
        var tasks = TryGetBoard(loaderUid, out var board) ? board.Tasks : ent.Comp.Tasks;
        var state = new NanoTaskUiState(tasks);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }

    // Starlight - Tidr: push the current board to every NanoTask cartridge on the given station
    private void RefreshStation(EntityUid station)
    {
        var board = EnsureComp<TidrBoardComponent>(station);
        var state = new NanoTaskUiState(board.Tasks);
        var query = EntityQueryEnumerator<NanoTaskCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out _, out _, out var cart))
        {
            if (cart.LoaderUid is { } loader && _stationSystem.GetOwningStation(loader) == station)
                _cartridgeLoader.UpdateCartridgeUiState(loader, state);
        }
    }

    // Starlight - Tidr: resolve the station-wide shared task board from any entity on the station
    private bool TryGetBoard(EntityUid source, out TidrBoardComponent board)
    {
        board = default!;
        if (_stationSystem.GetOwningStation(source) is not { } station)
            return false;
        board = EnsureComp<TidrBoardComponent>(station);
        return true;
    }

    // Starlight - Tidr: read the ID card currently inserted in the PDA running this cartridge
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

    // Starlight - Tidr: true if the card inserted in this PDA is the one that posted the task
    private bool IsTaskOwner(TidrBoardComponent board, int taskId, EntityUid loader)
    {
        return TryGetInsertedId(loader, out var card)
            && board.Owners.TryGetValue(taskId, out var owner)
            && owner == card;
    }
}
