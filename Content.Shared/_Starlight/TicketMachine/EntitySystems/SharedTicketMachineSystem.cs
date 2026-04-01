using Content.Shared._Starlight.TicketMachine.Components;
using Content.Shared.Interaction;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Power;
using Robust.Shared.Audio.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Examine;
using Robust.Shared.Timing;
using Robust.Shared.Containers;
using System.Linq;
using Content.Shared.Stacks;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.TicketMachine.EntitySystems;

public abstract class SharedTicketMachineSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _powerReceiverSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Tickets issue

        SubscribeLocalEvent<TicketMachineComponent, AfterInteractUsingEvent>(OnInteract);
        SubscribeLocalEvent<TicketMachineComponent, InteractHandEvent>(OnHandInteract);

        // Visuals
        SubscribeLocalEvent<TicketMachineComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<TicketMachineComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<TicketMachineComponent, EntRemovedFromContainerMessage>(OnEjected);
        SubscribeLocalEvent<TicketMachineComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<TicketComponent, ExaminedEvent>(OnTicketExamined);
    }

    #region Ticket Issuing

    /// <summary>
    /// Handles interaction with the ticket machine using an id card.
    /// </summary>
    private void OnInteract(EntityUid uid, TicketMachineComponent component, AfterInteractUsingEvent args)
    {
        if (!_gameTiming.IsFirstTimePredicted || args.Handled || !args.CanReach)
            return;

        if (TryComp<AccessReaderComponent>(uid, out var accessReader) && HasComp<IdCardComponent>(args.Used))
        {
            if (!_accessReaderSystem.IsAllowed(args.Used, uid, accessReader))
            {
                args.Handled = true;
                _audioSystem.PlayPredicted(component.accessDeniedSound, uid, args.User);
                return;
            }
            component.dispenseEnabled = !component.dispenseEnabled;
            _popupSystem.PopupPredicted(Loc.GetString("ticket-machine-dispense-toggled"), args.User, null, PopupType.Medium);
            args.Handled = true;
        }
    }

    /// <summary>
    /// Handles interaction with the ticket machine with empty hand.
    /// </summary>
    private void OnHandInteract(EntityUid uid, TicketMachineComponent component, InteractHandEvent args)
    {
        if (!_gameTiming.IsFirstTimePredicted || args.Handled 
            || !CanIssueTicket(uid, component, out var paper))
            return;

        component.previousIssueTime = _gameTiming.CurTime;

        if (!component.dispenseEnabled)
        {
            _popupSystem.PopupPredicted(Loc.GetString("ticket-machine-dispense-disabled"), args.User, null, PopupType.Medium);
            args.Handled = true;
            return;
        }
        
        var ticket = EntityManager.PredictedSpawnAtPosition(component.TicketProtoId, Transform(uid).Coordinates);
        args.Handled = true;
        
        if (TryComp<TicketComponent>(ticket, out var ticketComponent))
        {
            component.lastIssuedNumber++;
            ticketComponent.Number = component.lastIssuedNumber;
            component.issuedTickets.Add(ticket);
            _audioSystem.PlayPredicted(component.dispenseSound, uid, args.User);
            _handsSystem.TryPickup(args.User, ticket);
            UpdateVisuals(uid, component);
            UpdateTicketVisuals(ticket, ticketComponent);
            if (paper != null && TryComp<StackComponent>(paper, out var stack))
                stack.Count--;
        }
        else
            QueueDel(ticket);
    }

    private bool CanIssueTicket(EntityUid uid, TicketMachineComponent component, [NotNullWhen(true)] out EntityUid? paper)
    {
        paper = null;
        if (component.lastIssuedNumber >= component.maxTickets || !component.dispenseEnabled || !_powerReceiverSystem.IsPowered(uid))
            return false;

        if (component.previousIssueTime + component.issueCooldown > _gameTiming.CurTime)
            return false;

        if (!_containerSystem.TryGetContainer(uid, component.PaperContainerId, out var container))
            return false;

        if (container.ContainedEntities.Count == 0)
            return false;
        
        if (container.ContainedEntities.First() is not { Valid: true } paperEntity)
            return false;
        else
            paper = paperEntity;

        if (TryComp<StackComponent>(paper, out var stack) && stack.Count <= 0)
            return false; // No paper

        return true;
    }

    #endregion

    #region Visuals

    /// <summary>
    /// Handles power state changes, for updating visuals.
    /// </summary>
    private void OnPowerChanged(EntityUid uid, TicketMachineComponent component, ref PowerChangedEvent args) => UpdateVisuals(uid, component);

    /// <summary>
    /// Updates the ticket machine's visuals. Protected for use in server/client side systems.
    /// </summary>
    protected void UpdateVisuals(EntityUid uid, TicketMachineComponent component)
    {
        int paperState = 3;
        if (!_containerSystem.TryGetContainer(uid, component.PaperContainerId, out var container))
            return;
        if (container.ContainedEntities.Count == 0)
            paperState = 3; // Empty
        else if (TryComp<StackComponent>(container.ContainedEntities.First(), out var stack) && _prototypeManager.TryIndex<StackPrototype>(stack.StackTypeId, out var proto))
            paperState = CalculatePaperState(proto.MaxCount == null ? 999 : proto.MaxCount.Value, stack.Count, component.paperStateAmount);

        _appearanceSystem.SetData(uid, TicketMachineVisuals.isPowered, _powerReceiverSystem.IsPowered(uid));
        _appearanceSystem.SetData(uid, TicketMachineVisuals.isFilled, container.ContainedEntities.Count > 0);
        _appearanceSystem.SetData(uid, TicketMachineVisuals.Paper, paperState);
        _appearanceSystem.SetData(uid, TicketMachineVisuals.DisplayNumber, component.displayNumber);
    }

    private void UpdateTicketVisuals(EntityUid uid, TicketComponent component) => _appearanceSystem.SetData(uid, TicketVisuals.Number, component.Number);

    private int CalculatePaperState(int maxTickets, int currentAmount, int paperStates)
    {
        if (paperStates <= 0 || maxTickets <= 0)
            return 1;

        if (currentAmount <= 0)
            return paperStates;

        float percent = (float)currentAmount / (float)maxTickets;

        if (percent >= 1f) return 1;

        int state = (int)Math.Floor(Math.Log(1f / percent, 2)) + 1;
        return Math.Clamp(state, 1, paperStates);
    }

    private void OnExamined(EntityUid uid, TicketMachineComponent component, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;
        args.PushMarkup(Loc.GetString("ticket-machine-displayed-ticket", ("number", component.displayNumber)));
    }

    private void OnTicketExamined(EntityUid uid, TicketComponent component, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;
        args.PushMarkup(Loc.GetString("ticket-machine-ticket-number", ("number", component.Number.ToString())));
    }

    private void OnEjected(EntityUid uid, TicketMachineComponent component, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != component.PaperContainerId)
            return;
        UpdateVisuals(uid, component);
    }

    private void OnInserted(EntityUid uid, TicketMachineComponent component, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != component.PaperContainerId)
            return;
        UpdateVisuals(uid, component);
    }

    #endregion
}
