using System.Linq;
using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Storage;
using Content.Shared.UserInterface;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

// ReSharper disable CheckNamespace
namespace Content.Shared.Chemistry.EntitySystems;

[UsedImplicitly]
public abstract partial class SharedReagentDispenserSystem : EntitySystem
{
    [Dependency] protected IPrototypeManager _prototypeManager = default!;
    [Dependency] protected IGameTiming _timing = default!;
    [Dependency] protected SharedAudioSystem _audioSystem = default!;
    [Dependency] protected SharedBatterySystem _battery = default!;
    [Dependency] protected SharedContainerSystem _container = default!;
    [Dependency] protected SharedHandsSystem _handsSystem = default!;
    [Dependency] protected ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] protected OpenableSystem _openable = default!;
    [Dependency] protected SharedPopupSystem _popup = default!;
    [Dependency] protected PowerCellSystem _powercell = default!;
    [Dependency] protected SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] protected SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] protected SolutionTransferSystem _solutionTransferSystem = default!;
    [Dependency] protected SharedUserInterfaceSystem _userInterfaceSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserSetDispenseAmountMessage>(OnSetDispenseAmountMessage);
        SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserDispenseReagentMessage>(OnDispenseReagentMessage);
        SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserEjectContainerMessage>(OnEjectReagentMessage);
        SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserClearContainerSolutionMessage>(OnClearContainerSolutionMessage);
        SubscribeLocalEvent<ReagentDispenserComponent, MapInitEvent>(OnMapInit, before: new[] { typeof(ItemSlotsSystem) });
        SubscribeLocalEvent<ReagentDispenserComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<ReagentDispenserComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<ReagentDispenserComponent, PowerCellSlotEmptyEvent>(OnPowerCellSlotEmpty);
        SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserToggleValveMessage>(OnToggleValveMessage);
        SubscribeLocalEvent<ReagentDispenserComponent, RefreshChargeRateEvent>(OnRefreshChargeRate);
        SubscribeLocalEvent<ReagentDispenserComponent, BoundUIOpenedEvent>(SubscribeRefreshBattery);
        SubscribeLocalEvent<ReagentDispenserComponent, BoundUIClosedEvent>(SubscribeRefreshBattery);
    }

    protected virtual void UpdateUi(Entity<ReagentDispenserComponent> reagentDispenser)
    {
    }

    private void ClickSound(Entity<ReagentDispenserComponent> reagentDispenser, EntityUid? user) =>
        _audioSystem.PlayPredicted(reagentDispenser.Comp.ClickSound, reagentDispenser, user, AudioParams.Default.WithVolume(-2f));

    private void OnClearContainerSolutionMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserClearContainerSolutionMessage message)
    {
        var outputContainer = _itemSlotsSystem.GetItemOrNull(reagentDispenser, SharedReagentDispenser.OutputSlotName);
        if (outputContainer is not { Valid: true } || !_solutionContainerSystem.TryGetFitsInDispenser(outputContainer.Value, out var solution, out _))
            return;

        _solutionContainerSystem.RemoveAllSolution(solution.Value);
        UpdateUi(reagentDispenser);
        ClickSound(reagentDispenser, message.Actor);
    }

    private void OnDestruction(EntityUid uid, ReagentDispenserComponent component, DestructionEventArgs args)
    {
        if (TryComp<StorageComponent>(uid, out var storage))
            _container.EmptyContainer(storage.Container, destination: Transform(uid).Coordinates);
    }

    private void OnDispenseReagentMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserDispenseReagentMessage message)
    {
        if (!TryComp<StorageComponent>(reagentDispenser.Owner, out var storage))
        {
            return;
        }

        var outputContainer = _itemSlotsSystem.GetItemOrNull(reagentDispenser, SharedReagentDispenser.OutputSlotName);
        if (outputContainer is not { Valid: true })
        {
            _popup.PopupClient(Loc.GetString("reagent-dispenser-window-no-container-loaded-text"), message.Actor);

            ClickSound(reagentDispenser, message.Actor);
            return;
        }

        if (!_solutionContainerSystem.TryGetFitsInDispenser(outputContainer.Value, out var solution, out _))
            return;

        // Ensure that the reagent is something this reagent dispenser can dispense.
        var storageLocation = message.Data.StorageLocation; // Starlight-edit
        var storedContainer = storage.StoredItems.FirstOrDefault(kvp => kvp.Value == storageLocation).Key;
        if (storedContainer != EntityUid.Invalid)
        {
            if (_solutionContainerSystem.TryGetDrainableSolution(storedContainer, out var src, out _) &&
                _solutionContainerSystem.TryGetRefillableSolution(outputContainer.Value, out var dst, out var dstSolution)) // Starlight edit
            {
                var transferAmount = FixedPoint2.New((int)reagentDispenser.Comp.DispenseAmount);
                if (dstSolution.AvailableVolume < transferAmount)
                {
                    // Not enough space in container
                    _popup.PopupClient(Loc.GetString("reagent-dispenser-component-cannot-fit-message"), message.Actor);

                    UpdateUi(reagentDispenser);
                    ClickSound(reagentDispenser, message.Actor);
                    return;
                }

                // force open container, if applicable, to avoid confusing people on why it doesn't dispense
                _openable.SetOpen(storedContainer, true);
                _solutionTransferSystem.Transfer(new SolutionTransferData(reagentDispenser,
                        storedContainer, src.Value,
                        outputContainer.Value, dst.Value,
                        (int)reagentDispenser.Comp.DispenseAmount));
            }
        }

        if (message.Data.ReagentID is { } reagentID && reagentDispenser.Comp.GeneratableReagents.TryGetValue(reagentID, out var powerDrain))
        {
            if (!_solutionContainerSystem.TryGetRefillableSolution(outputContainer.Value, out var dst, out var dstSolution))
                return;

            // Check if there's enough space in the container FIRST (before checking power)
            var amountToDispense = FixedPoint2.New((int)reagentDispenser.Comp.DispenseAmount);
            if (dstSolution.AvailableVolume < amountToDispense)
            {
                // Not enough space in container
                _popup.PopupClient(Loc.GetString("reagent-dispenser-component-cannot-fit-message"), message.Actor);

                UpdateUi(reagentDispenser);
                ClickSound(reagentDispenser, message.Actor);
                return;
            }

            // Check if there's enough power BEFORE dispensing
            if (!_powercell.HasCharge(reagentDispenser.Owner, powerDrain * (float)reagentDispenser.Comp.DispenseAmount))
            {
                if (reagentDispenser.Comp.NoEnergyPopup is { } popup)
                    _popup.PopupClient(Loc.GetString(popup), message.Actor);

                UpdateUi(reagentDispenser);
                ClickSound(reagentDispenser, message.Actor);
                return;
            }

            // Try to add the reagent
            if (!_solutionContainerSystem.TryAddReagent(dst.Value, reagentID.ToString(), amountToDispense))
            {
                // Failed to add reagent (shouldn't happen since we checked space, but just in case)
                _popup.PopupClient(Loc.GetString("reagent-dispenser-component-cannot-fit-message"), message.Actor);

                UpdateUi(reagentDispenser);
                ClickSound(reagentDispenser, message.Actor);
                return;
            }

            // Successfully dispensed, now use the power
            if (!_powercell.TryUseCharge(reagentDispenser.Owner, powerDrain * (float)reagentDispenser.Comp.DispenseAmount))
            {
                // This shouldn't happen since we already checked HasCharge, but log it just in case
                Log.Warning($"Failed to use power charge on dispenser {ToPrettyString(reagentDispenser.Owner)} after dispensing reagent");
            }
        }

        UpdateUi(reagentDispenser);
        ClickSound(reagentDispenser, message.Actor);
    }

    private void OnEjectReagentMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserEjectContainerMessage message)
    {
        if (!TryComp<StorageComponent>(reagentDispenser.Owner, out var storage))
        {
            return;
        }

        var storageLocation = message.StorageLocation;
        var storedContainer = storage.StoredItems.FirstOrDefault(kvp => kvp.Value == storageLocation).Key;
        if (storedContainer == EntityUid.Invalid)
            return;

        _handsSystem.TryPickupAnyHand(message.Actor, storedContainer);
    }

    private void OnMapInit(Entity<ReagentDispenserComponent> ent, ref MapInitEvent args) =>
        _itemSlotsSystem.AddItemSlot(ent.Owner, SharedReagentDispenser.OutputSlotName, ent.Comp.BeakerSlot);

    private void OnPowerCellChanged(Entity<ReagentDispenserComponent> ent, ref PowerCellChangedEvent args)
    {
        if (!args.Ejected)
            return;

        UpdateUi(ent);
    }

    private void OnPowerCellSlotEmpty(Entity<ReagentDispenserComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        UpdateUi(ent);

        // Close the UI when cell is ejected
        if (TryComp<ActivatableUIComponent>(ent.Owner, out var activatable) && activatable.Key != null)
            _userInterfaceSystem.CloseUi(ent.Owner, activatable.Key);
    }

    private void OnRefreshChargeRate(Entity<ReagentDispenserComponent> ent, ref RefreshChargeRateEvent args)
    {
        if (_powerReceiver.IsPowered(ent.Owner))
            args.NewChargeRate += 5f;
        else if (_userInterfaceSystem.IsUiOpen(ent.Owner, ReagentDispenserUiKey.Key))
            args.NewChargeRate -= 5f;
    }

    private void OnSetDispenseAmountMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserSetDispenseAmountMessage message)
    {
        reagentDispenser.Comp.DispenseAmount = message.ReagentDispenserDispenseAmount;
        Dirty(reagentDispenser);
        UpdateUi(reagentDispenser);
        ClickSound(reagentDispenser, message.Actor);
    }

    private void OnToggleValveMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserToggleValveMessage message)
    {
        if (!TryComp<PlumbingOutletComponent>(reagentDispenser.Owner, out var plumbingOutlet))
            return;

        plumbingOutlet.Enabled = !plumbingOutlet.Enabled;
        Dirty(reagentDispenser.Owner, plumbingOutlet);
        UpdateUi(reagentDispenser);
        ClickSound(reagentDispenser, message.Actor);
    }

    private void RefreshBatteryChargeRate(Entity<ReagentDispenserComponent> ent)
    {
        if (_timing.ApplyingState)
            return;

        if (_powercell.TryGetBatteryFromSlot(ent.Owner, out var battery))
            _battery.RefreshChargeRate((battery.Value.Owner, battery.Value.Comp));
    }

    private void SubscribeRefreshBattery<T>(Entity<ReagentDispenserComponent> ent, ref T ev) => RefreshBatteryChargeRate(ent);
}
