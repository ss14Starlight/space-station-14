using System.Linq;
using Content.Server.Chemistry.Components;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Storage.EntitySystems;
using JetBrains.Annotations;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Shared.Labels.Components;
using Content.Shared.Storage;
using Content.Server.Hands.Systems;
// Starlight Start
using Content.Shared.PowerCell;
using Content.Shared.Destructible;
using Content.Shared.PowerCell.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Server.PowerCell;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Shared.UserInterface;
// Starlight end

namespace Content.Server.Chemistry.EntitySystems
{
    /// <summary>
    /// Contains all the server-side logic for reagent dispensers.
    /// <seealso cref="ReagentDispenserComponent"/>
    /// </summary>
    [UsedImplicitly]
    public sealed class ReagentDispenserSystem : EntitySystem
    {
        [Dependency] private readonly AudioSystem _audioSystem = default!;
        [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
        [Dependency] private readonly SolutionTransferSystem _solutionTransferSystem = default!;
        [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly OpenableSystem _openable = default!;
        [Dependency] private readonly HandsSystem _handsSystem = default!;
        
        // Starlight-start
        [Dependency] private readonly PowerCellSystem _powercell = default!;
        [Dependency] private readonly SharedContainerSystem _container = default!;
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly BatterySystem _battery = default!;
        private readonly Dictionary<EntityUid, float> _uiUpdateAccumulators = new();
        private const float UiUpdateInterval = 0.5f;
        // Starlight-end

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<ReagentDispenserComponent, ComponentStartup>(SubscribeUpdateUiState);
            SubscribeLocalEvent<ReagentDispenserComponent, SolutionContainerChangedEvent>(SubscribeUpdateUiState);
            SubscribeLocalEvent<ReagentDispenserComponent, EntInsertedIntoContainerMessage>(SubscribeUpdateUiState, after: [typeof(SharedStorageSystem)]);
            SubscribeLocalEvent<ReagentDispenserComponent, EntRemovedFromContainerMessage>(SubscribeUpdateUiState, after: [typeof(SharedStorageSystem)]);
            SubscribeLocalEvent<ReagentDispenserComponent, BoundUIOpenedEvent>(SubscribeUpdateUiState);

            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserSetDispenseAmountMessage>(OnSetDispenseAmountMessage);
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserDispenseReagentMessage>(OnDispenseReagentMessage);
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserEjectContainerMessage>(OnEjectReagentMessage);
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserClearContainerSolutionMessage>(OnClearContainerSolutionMessage);

            SubscribeLocalEvent<ReagentDispenserComponent, MapInitEvent>(OnMapInit, before: new[] { typeof(ItemSlotsSystem) });
            // Starlight Start
            SubscribeLocalEvent<ReagentDispenserComponent, ComponentRemove>(OnComponentRemove);
            SubscribeLocalEvent<ReagentDispenserComponent, PowerCellChangedEvent>(OnPowerCellChanged);
            SubscribeLocalEvent<ReagentDispenserComponent, DestructionEventArgs>(OnDestruction);
            SubscribeLocalEvent<ReagentDispenserComponent, PowerCellSlotEmptyEvent>(OnPowerCellSlotEmpty);
            // Starlight End
        }

        // Starlight Start: Reagent Dispensers use cells
        #region Starlight
        private void OnComponentRemove(EntityUid uid, ReagentDispenserComponent component, ComponentRemove args)
        {
            _uiUpdateAccumulators.Remove(uid);
        }

        // Recharge power cell from APC power
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<ReagentDispenserComponent, PowerCellSlotComponent, ApcPowerReceiverComponent, ActivatableUIComponent>();
            while (query.MoveNext(out var uid, out var dispenser, out var cellSlot, out var powerReceiver, out var activatableUI))
            {
                if (!_powercell.TryGetBatteryFromSlot(uid, out var batteryUid, out var battery, cellSlot))
                    continue;

                float chargeRate;
                bool isChargingOrDraining = false;

                // Check if UI is open
                var uiOpen = activatableUI.Key != null && _userInterfaceSystem.IsUiOpen(uid, activatableUI.Key);

                if (powerReceiver.Powered)
                {
                    // Charge at 5W when connected to power
                    chargeRate = 5f;
                    
                    if (_battery.IsFull(batteryUid.Value, battery))
                        continue;
                    
                    isChargingOrDraining = true;
                }
                else if (uiOpen)
                {
                    // Drain at 5W when UI is open and not powered by APC
                    chargeRate = -5f;
                    
                    if (battery.CurrentCharge <= 0)
                    {
                        // Close UI if cell is dead
                        if (activatableUI.Key != null)
                            _userInterfaceSystem.CloseUi(uid, activatableUI.Key);
                        continue;
                    }
                    
                    isChargingOrDraining = true;
                }
                else
                {
                    continue;
                }

                if (chargeRate > 0 && battery.CurrentCharge + (chargeRate * frameTime) > battery.MaxCharge)
                {
                    if (uiOpen)
                    {
                        if (!_uiUpdateAccumulators.ContainsKey(uid))
                            _uiUpdateAccumulators[uid] = 0f;

                        _uiUpdateAccumulators[uid] += frameTime;
                        if (_uiUpdateAccumulators[uid] >= UiUpdateInterval)
                        {
                            _uiUpdateAccumulators[uid] = 0f;
                            UpdateEnergyBar((uid, dispenser));
                        }
                    }
                    continue;
                }

                _battery.ChangeCharge(batteryUid.Value, chargeRate * frameTime, battery);
                
                if (chargeRate < 0 && battery.CurrentCharge <= 0 && uiOpen)
                {
                    UpdateUiState((uid, dispenser));
                    if (activatableUI.Key != null)
                        _userInterfaceSystem.CloseUi(uid, activatableUI.Key);
                }
                // Prevent entire UI flashing when charging/draining
                else if (isChargingOrDraining && uiOpen)
                {
                    if (!_uiUpdateAccumulators.ContainsKey(uid))
                        _uiUpdateAccumulators[uid] = 0f;

                    _uiUpdateAccumulators[uid] += frameTime;
                    if (_uiUpdateAccumulators[uid] >= UiUpdateInterval)
                    {
                        _uiUpdateAccumulators[uid] = 0f;
                        UpdateEnergyBar((uid, dispenser));
                    }
                }
            }
        }

        private void UpdateEnergyBar(Entity<ReagentDispenserComponent> reagentDispenser)
        {
            if (!_powercell.TryGetBatteryFromSlot(reagentDispenser.Owner, out var battery))
                return;

            var energy = battery.CurrentCharge / battery.MaxCharge;
            var message = new ReagentDispenserEnergyUpdateMessage(energy);
            _userInterfaceSystem.ServerSendUiMessage(reagentDispenser.Owner, ReagentDispenserUiKey.Key, message);
        }
        private void OnDestruction(EntityUid uid, ReagentDispenserComponent component, DestructionEventArgs args)
        {
            if (TryComp<StorageComponent>(uid, out var storage))
                _container.EmptyContainer(storage.Container, destination: Transform(uid).Coordinates);
        }

        private void OnPowerCellChanged(Entity<ReagentDispenserComponent> ent, ref PowerCellChangedEvent args)
        {
            if (!args.Ejected)
                return;

            UpdateUiState(ent);
            
            if (!_powercell.HasActivatableCharge(ent.Owner))
            {
                if (TryComp<ActivatableUIComponent>(ent.Owner, out var activatable) && activatable.Key != null)
                    _userInterfaceSystem.CloseUi(ent.Owner, activatable.Key);
            }
        }

        private void OnPowerCellSlotEmpty(Entity<ReagentDispenserComponent> ent, ref PowerCellSlotEmptyEvent args)
        {
            UpdateUiState(ent);

            // Close the UI when cell is ejected
            if (TryComp<ActivatableUIComponent>(ent.Owner, out var activatable) && activatable.Key != null)
                _userInterfaceSystem.CloseUi(ent.Owner, activatable.Key);
        }
        #endregion
        // Starlight End

        private void SubscribeUpdateUiState<T>(Entity<ReagentDispenserComponent> ent, ref T ev)
        {
            UpdateUiState(ent);
        }

        private void UpdateUiState(Entity<ReagentDispenserComponent> reagentDispenser)
        {
            var outputContainer = _itemSlotsSystem.GetItemOrNull(reagentDispenser, SharedReagentDispenser.OutputSlotName);
            var outputContainerInfo = BuildOutputContainerInfo(outputContainer);

            var inventory = GetInventory(reagentDispenser);

            var energy = _powercell.TryGetBatteryFromSlot(reagentDispenser.Owner, out var battery) ? battery.CurrentCharge / battery.MaxCharge : 0f; // Starlight-edit: Energy bar

            var state = new ReagentDispenserBoundUserInterfaceState(outputContainerInfo, GetNetEntity(outputContainer), inventory, reagentDispenser.Comp.DispenseAmount, energy); // Starlight-edit: Energy bar
            _userInterfaceSystem.SetUiState(reagentDispenser.Owner, ReagentDispenserUiKey.Key, state);
        }

        private ContainerInfo? BuildOutputContainerInfo(EntityUid? container)
        {
            if (container is not { Valid: true })
                return null;

            if (_solutionContainerSystem.TryGetFitsInDispenser(container.Value, out _, out var solution))
            {
                return new ContainerInfo(Name(container.Value), solution.Volume, solution.MaxVolume)
                {
                    Reagents = solution.Contents
                };
            }

            return null;
        }

        private List<ReagentInventoryItem> GetInventory(Entity<ReagentDispenserComponent> reagentDispenser)
        {
            if (!TryComp<StorageComponent>(reagentDispenser.Owner, out var storage))
            {
                return [];
            }

            var inventory = new List<ReagentInventoryItem>();

            foreach (var (storedContainer, storageLocation) in storage.StoredItems)
            {
                string reagentLabel;
                if (TryComp<LabelComponent>(storedContainer, out var label) && !string.IsNullOrEmpty(label.CurrentLabel))
                    reagentLabel = label.CurrentLabel;
                else
                    reagentLabel = Name(storedContainer);

                // Get volume remaining and color of solution
                FixedPoint2 quantity = 0f;
                var reagentColor = Color.White;
                if (_solutionContainerSystem.TryGetDrainableSolution(storedContainer, out _, out var sol))
                {
                    quantity = sol.Volume;
                    reagentColor = sol.GetColor(_prototypeManager);
                }

                var data = new ReagentDispenseData(storageLocation, null); // Starlight-edit
                inventory.Add(new ReagentInventoryItem(data, reagentLabel, quantity, reagentColor, false)); // Starlight-edit
            }
            
            // Starlight-start: Generatable Reagents
            foreach (var (reagent, powerDrain) in reagentDispenser.Comp.GeneratableReagents)
            {
                if (_prototypeManager.TryIndex<ReagentPrototype>(reagent, out var reagentPrototype))
                {
                    FixedPoint2 quantity = 100f;
                    var data = new ReagentDispenseData(null, reagent);
                    inventory.Add(new ReagentInventoryItem(data, reagentPrototype.LocalizedName, quantity, reagentPrototype.SubstanceColor, true));
                }
            }
            // Starlight-end

            return inventory;
        }

        private void OnSetDispenseAmountMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserSetDispenseAmountMessage message)
        {
            reagentDispenser.Comp.DispenseAmount = message.ReagentDispenserDispenseAmount;
            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
        }

        private void OnDispenseReagentMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserDispenseReagentMessage message)
        {
            if (!TryComp<StorageComponent>(reagentDispenser.Owner, out var storage))
                return;

            // Starlight Start
            var outputContainer = _itemSlotsSystem.GetItemOrNull(reagentDispenser, SharedReagentDispenser.OutputSlotName);
            if (outputContainer is not { Valid: true })
            {
                if (TryComp<UserInterfaceComponent>(reagentDispenser.Owner, out var ui)
                    && ui.Actors is { } actors
                    && actors.TryGetValue(ReagentDispenserUiKey.Key, out var entities))
                    foreach (var entity in entities)
                        _popup.PopupCursor(Loc.GetString("reagent-dispenser-window-no-container-loaded-text"), entity);
                
                ClickSound(reagentDispenser);
                return;
            }

            if (!_solutionContainerSystem.TryGetFitsInDispenser(outputContainer.Value, out var solution, out _))
                return;
            // Starlight End

            // Ensure that the reagent is something this reagent dispenser can dispense.
            var storageLocation = message.Data.StorageLocation; // Starlight-edit
            var storedContainer = storage.StoredItems.FirstOrDefault(kvp => kvp.Value == storageLocation).Key;
            if (storedContainer != EntityUid.Invalid)
            { // Starlight-edit
                // Starlight edit Start: Moved
                // var outputContainer = _itemSlotsSystem.GetItemOrNull(reagentDispenser, SharedReagentDispenser.OutputSlotName);
                // if (outputContainer is not { Valid: true } || !_solutionContainerSystem.TryGetFitsInDispenser(outputContainer.Value, out var solution, out _))
                //     return;
                // Starlight edit End
                if (_solutionContainerSystem.TryGetDrainableSolution(storedContainer, out var src, out _) &&
                    _solutionContainerSystem.TryGetRefillableSolution(outputContainer.Value, out var dst, out var dstSolution)) // Starlight edit
                {
                    // Starlight Start
                    var transferAmount = FixedPoint2.New((int)reagentDispenser.Comp.DispenseAmount);
                    if (dstSolution.AvailableVolume < transferAmount)
                    {
                        // Not enough space in container
                        if (TryComp<UserInterfaceComponent>(reagentDispenser.Owner, out var ui)
                            && ui.Actors is { } actors
                            && actors.TryGetValue(ReagentDispenserUiKey.Key, out var entities))
                            foreach (var entity in entities)
                                _popup.PopupCursor(Loc.GetString("reagent-dispenser-component-cannot-fit-message"), entity);

                        UpdateUiState(reagentDispenser);
                        ClickSound(reagentDispenser);
                        return;
                    }
                    // Starlight End

                    // force open container, if applicable, to avoid confusing people on why it doesn't dispense
                    _openable.SetOpen(storedContainer, true);
                    _solutionTransferSystem.Transfer(reagentDispenser,
                            storedContainer, src.Value,
                            outputContainer.Value, dst.Value,
                            (int)reagentDispenser.Comp.DispenseAmount);
                }
            }

            // Starlight-start: Generatable Reagents
            if (message.Data.ReagentID is { } reagentID && reagentDispenser.Comp.GeneratableReagents.TryGetValue(reagentID, out var powerDrain))
            {
                if (!_solutionContainerSystem.TryGetRefillableSolution(outputContainer.Value, out var dst, out var dstSolution))
                    return;

                // Check if there's enough space in the container FIRST (before checking power)
                var amountToDispense = FixedPoint2.New((int)reagentDispenser.Comp.DispenseAmount);
                if (dstSolution.AvailableVolume < amountToDispense)
                {
                    // Not enough space in container
                    if (TryComp<UserInterfaceComponent>(reagentDispenser.Owner, out var ui)
                        && ui.Actors is { } actors
                        && actors.TryGetValue(ReagentDispenserUiKey.Key, out var entities))
                        foreach (var entity in entities)
                            _popup.PopupCursor(Loc.GetString("reagent-dispenser-component-cannot-fit-message"), entity);
                    
                    UpdateUiState(reagentDispenser);
                    ClickSound(reagentDispenser);
                    return;
                }

                // Check if there's enough power BEFORE dispensing
                if (!_powercell.HasCharge(reagentDispenser.Owner, powerDrain * (float)reagentDispenser.Comp.DispenseAmount))
                {
                    if (reagentDispenser.Comp.NoEnergyPopup is { } popup
                        && TryComp<UserInterfaceComponent>(reagentDispenser.Owner, out var ui2)
                        && ui2.Actors is { } actors2
                        && actors2.TryGetValue(ReagentDispenserUiKey.Key, out var entities2))
                        foreach (var entity in entities2)
                            _popup.PopupCursor(Loc.GetString(popup), entity);
                    
                    UpdateUiState(reagentDispenser);
                    ClickSound(reagentDispenser);
                    return;
                }

                // Try to add the reagent
                if (!_solutionContainerSystem.TryAddReagent(dst.Value, reagentID.ToString(), amountToDispense))
                {
                    // Failed to add reagent (shouldn't happen since we checked space, but just in case)
                    if (TryComp<UserInterfaceComponent>(reagentDispenser.Owner, out var ui3)
                        && ui3.Actors is { } actors3
                        && actors3.TryGetValue(ReagentDispenserUiKey.Key, out var entities3))
                        foreach (var entity in entities3)
                            _popup.PopupCursor(Loc.GetString("reagent-dispenser-component-cannot-fit-message"), entity);
                    
                    UpdateUiState(reagentDispenser);
                    ClickSound(reagentDispenser);
                    return;
                }
                
                // Successfully dispensed, now use the power
                if (!_powercell.TryUseCharge(reagentDispenser.Owner, powerDrain * (float)reagentDispenser.Comp.DispenseAmount))
                {
                    // This shouldn't happen since we already checked HasCharge, but log it just in case
                    Logger.Warning($"Failed to use power charge on dispenser {ToPrettyString(reagentDispenser.Owner)} after dispensing reagent");
                }
            }

            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
            // Starlight-end
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

        private void OnClearContainerSolutionMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserClearContainerSolutionMessage message)
        {
            var outputContainer = _itemSlotsSystem.GetItemOrNull(reagentDispenser, SharedReagentDispenser.OutputSlotName);
            if (outputContainer is not { Valid: true } || !_solutionContainerSystem.TryGetFitsInDispenser(outputContainer.Value, out var solution, out _))
                return;

            _solutionContainerSystem.RemoveAllSolution(solution.Value);
            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
        }

        private void ClickSound(Entity<ReagentDispenserComponent> reagentDispenser)
        {
            _audioSystem.PlayPvs(reagentDispenser.Comp.ClickSound, reagentDispenser, AudioParams.Default.WithVolume(-2f));
        }

        /// <summary>
        /// Initializes the beaker slot
        /// </summary>
        private void OnMapInit(Entity<ReagentDispenserComponent> ent, ref MapInitEvent args)
        {
            _itemSlotsSystem.AddItemSlot(ent.Owner, SharedReagentDispenser.OutputSlotName, ent.Comp.BeakerSlot);
        }
    }
}
