using Content.Server.Construction;
using Content.Shared.Construction;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared.Atmos.Components;
using Content.Shared.RCD.Components;
using Content.Shared.RCD.Systems;
using Content.Shared._Starlight.Silicons;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Organ;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Examine;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Bed.Sleep;
using Content.Shared._DEN.QuickConstruction.Components;
using Content.Shared._DEN.QuickConstruction.Events;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.CombatMode;
using Content.Shared.Weapons.Melee;
using Robust.Server.Containers;
using Content.Server.Movement.Systems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Content.Server._Starlight.Computers.RemoteControl;

public sealed partial class RemoteControlConsoleSystem : EntitySystem
{
    // Dependency felder nicht read only. Ignoriere IDE0044.
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private SharedViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedBorgSystem _borg = default!;
    [Dependency] private PullController _pullController = default!;

    private readonly HashSet<EntityUid> _pendingBorgRefreshes = new();
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _remoteActionOverrides = new();
    private readonly HashSet<(EntityUid UiEntity, Enum UiKey, EntityUid Controller, EntityUid Console)> _pendingRemoteUiMirrors = new();
    private readonly ConcurrentDictionary<(EntityUid UiEntity, Enum UiKey, EntityUid Actor), byte> _remoteUiRangeOverrides = new();
    private readonly ISawmill _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("remote-control");

    public override void Initialize()
    {
        Subs.BuiEvents<RemoteControlConsoleComponent>(RemoteControlUIKey.Key,
            subs =>
            {
                subs.Event<BoundUIClosedEvent>(OnUiClosed);
                subs.Event<RemoteControlToggleMessage>(OnToggleControl);
                subs.Event<RemoteControlInteractionMessage>(OnRemoteInteraction);
                subs.Event<RemoteControlBuildConstructionMessage>(OnRemoteBuildConstruction);
                subs.Event<RemoteControlBuildItemConstructionMessage>(OnRemoteBuildItemConstruction);
                subs.Event<RemoteControlActionMessage>(OnRemoteAction);
                subs.Event<RemoteControlHandMessage>(OnRemoteHand);
                subs.Event<RemoteControlInventoryMessage>(OnRemoteInventory);
                subs.Event<RemoteControlStorageMessage>(OnRemoteStorage);
            });
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        _hands.OnHandSetActive += OnHandSetActive;
    }

    public override void Shutdown()
    {
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        _hands.OnHandSetActive -= OnHandSetActive;
    }

    private void OnHandSetActive(Entity<HandsComponent>? entity)
    {
        if (entity is not { } hands)
            return;

        var query = EntityQueryEnumerator<RemoteControlConsoleComponent>();
        while (query.MoveNext(out var consoleUid, out var console))
        {
            if (console.Controller is null
                || !TryGetRemoteEntity(console, out var remoteEntity)
                || remoteEntity != hands.Owner)
                continue;

            RefreshRemoteState(consoleUid, console, remoteEntity);
        }
    }

    private void RefreshRemoteContainerState(EntityUid containerOwner)
    {
        foreach (var console in EntityQuery<RemoteControlConsoleComponent>())
        {
            if (console.Controller is null
                || !TryGetRemoteEntity(console, out var controlledEntity)
                || controlledEntity != containerOwner)
                continue;

            RefreshRemoteState(console.Owner, console, controlledEntity);
        }
    }

    [SubscribeLocalEvent]
    private void OnRemoteContainerInserted(EntGotInsertedIntoContainerMessage args)
        => RefreshRemoteContainerState(args.Container.Owner);

    [SubscribeLocalEvent]
    private void OnRemoteContainerRemoved(EntGotRemovedFromContainerMessage args)
        => RefreshRemoteContainerState(args.Container.Owner);

    [SubscribeLocalEvent]
    private void OnRemoteBorgModuleSelected(Entity<SelectableBorgModuleComponent> module, ref BorgModuleSelectedEvent args)
        => _pendingBorgRefreshes.Add(args.Chassis);

    [SubscribeLocalEvent]
    private void OnRemoteBorgModuleUnselected(Entity<SelectableBorgModuleComponent> module, ref BorgModuleUnselectedEvent args)
        => _pendingBorgRefreshes.Add(args.Chassis);

    [SubscribeLocalEvent]
    private void OnRemoteBorgModuleInstalled(Entity<BorgModuleComponent> module, ref BorgModuleInstalledEvent args)
    {
        var selectable = TryComp<SelectableBorgModuleComponent>(module, out var selectableComponent);
        var action = selectable && selectableComponent is not null && selectableComponent.ModuleSwapActionEntity is { } actionEntity
            ? ToPrettyString(actionEntity)
            : "null";
        _pendingBorgRefreshes.Add(args.ChassisEnt);
    }

    [SubscribeLocalEvent]
    private void OnRemoteBorgModuleUninstalled(Entity<BorgModuleComponent> module, ref BorgModuleUninstalledEvent args)
        => _pendingBorgRefreshes.Add(args.ChassisEnt);

    [SubscribeLocalEvent]
    private void OnRemoteBrainRemoved(Entity<RemoteControlBrainComponent> brain, ref EntGotRemovedFromContainerMessage args)
    {
        if (!TryComp<BorgChassisComponent>(args.Container.Owner, out var chassis)
            || args.Container != chassis.BrainContainer)
            return;

        var query = EntityQueryEnumerator<RemoteControlConsoleComponent>();
        while (query.MoveNext(out var consoleUid, out var console))
        {
            if (console.RemoteBrain != brain.Owner)
                continue;

            Entity<RemoteControlConsoleComponent> consoleEntity = new(consoleUid, console);
            if (TryGetRemoteEntity(console, out var remoteEntity))
                CleanupUsers(consoleEntity, remoteEntity);

            SetController(consoleUid, console, null, args.Container.Owner);
            DeactivateRemoteBorg(console, args.Container.Owner);
            console.RemoteBrain = null;
            Dirty(consoleEntity);
            break;
        }
    }

    private void RefreshRemoteBorgModuleState(EntityUid chassis)
    {
        if (TryFindConsole(chassis, out var console, out _))
            RefreshRemoteState(console.Owner, console.Comp, chassis);
    }

    [SubscribeLocalEvent]
    private void OnNewLink(Entity<RemoteControlConsoleComponent> entity, ref NewLinkEvent args)
    {
        if (args.Source != entity.Owner || !HasComp<RemoteControlBrainComponent>(args.Sink))
            return;

        if (entity.Comp.RemoteBrain is not null && TryGetRemoteEntity(entity.Comp, out var oldRemoteEntity))
            CleanupUsers(entity, oldRemoteEntity);

        entity.Comp.RemoteBrain = args.Sink;
        Dirty(entity);
    }

    [SubscribeLocalEvent]
    private void OnUiOpen(EntityUid uid, RemoteControlConsoleComponent component, AfterActivatableUIOpenEvent args)
    {
        _sawmill.Debug($"[RemoteControl] server UI open console={uid} actor={args.User} users={string.Join(',', component.Users)} controller={component.Controller}");

        if (!TryGetRemoteEntity(component, out var remoteEntity))
        {
            _ui.SetUiState(uid, RemoteControlUIKey.Key, new RemoteControlConsoleBuiState
            {
                Connected = false,
                RemoteEntity = null,
                Controller = null,
                Actions = System.Array.Empty<NetEntity>(),
            SelectedAction = null,
                Hands = System.Array.Empty<RemoteControlHandState>(),
                Inventory = System.Array.Empty<RemoteControlInventorySlotState>(),
            });
            return;
        }

        component.Users.Add(args.User);

        if (_playerManager.TryGetSessionByEntity(args.User, out var session))
        {
            AddRemotePvsOverrides(remoteEntity, args.User, session);
            _viewSubscriber.AddViewSubscriber(remoteEntity, session);
        }
        var actionEntities = GetRemoteActionEntities(remoteEntity);
        var quickConstructionItem = GetRemoteQuickConstructionItem(remoteEntity);

        _ui.SetUiState(uid, RemoteControlUIKey.Key, new RemoteControlConsoleBuiState
        {
            Connected = true,
            RemoteEntity = GetNetEntity(remoteEntity),
            Controller = component.Controller is { } controller ? GetNetEntity(controller) : null,
            Actions = GetRemoteActions(actionEntities),
            SelectedAction = GetSelectedRemoteAction(remoteEntity),
            QuickConstructionItem = quickConstructionItem is { } item ? GetNetEntity(item) : null,
            Hands = GetRemoteHands(remoteEntity),
            Inventory = GetRemoteInventory(remoteEntity),
        });
    }

    private void OnToggleControl(EntityUid uid, RemoteControlConsoleComponent component, RemoteControlToggleMessage args)
    {
        if (!component.Users.Contains(args.Actor)
            || !TryGetRemoteEntity(component, out var remoteEntity))
            return;

        if (component.Controller == args.Actor)
        {
            SetController(uid, component, null, remoteEntity);
            return;
        }

        if (component.Controller != null || !_actionBlocker.CanConsciouslyPerformAction(args.Actor))
            return;

        SetController(uid, component, args.Actor, remoteEntity);
    }

    private void OnRemoteAction(EntityUid uid, RemoteControlConsoleComponent component, RemoteControlActionMessage args)
    {
        if (component.Controller != args.Actor || !TryGetRemoteEntity(component, out var remoteEntity))
            return;

        var action = GetEntity(args.Action);
        var availableActions = GetRemoteActionEntities(remoteEntity);
        if (!availableActions.Contains(action))
        {
            return;
        }

        if (TryComp<InstantActionComponent>(action, out var instantAction)
            && instantAction.Event is BorgToggleSelectTypeEvent
            && _actions.GetAction(action) is { } actionComponent)
        {
            _actions.PerformAction((remoteEntity, null), actionComponent, predicted: false);
            return;
        }

        _actions.TryPerformAction(remoteEntity, action);
    }

    private void OnRemoteInteraction(EntityUid uid, RemoteControlConsoleComponent component, RemoteControlInteractionMessage args)
    {
        if (!TryGetControlledEntity(component, args.Actor, out var remoteEntity))
            return;

        var target = args.Target is { } targetNet ? GetEntity(targetNet) : (EntityUid?) null;

        if (args.Action == RemoteControlInteractionAction.TryPull
            && target is { } pullTarget)
        {
            _interaction.TryPullObject(remoteEntity, pullTarget);
            RefreshRemoteState(uid, component, remoteEntity);
            return;
        }

        if (args.Action == RemoteControlInteractionAction.MovePulledObject)
        {
            _pullController.MovePulledObject(remoteEntity, GetCoordinates(args.Coordinates));
            RefreshRemoteState(uid, component, remoteEntity);
            return;
        }

        if (args.ActivateInWorld && target is { } activateTarget)
        {
            _interaction.InteractionActivate(remoteEntity, activateTarget, complexInteractions: true);
            RefreshRemoteState(uid, component, remoteEntity);
            return;
        }

        var activeItem = TryComp<HandsComponent>(remoteEntity, out var remoteHands)
            ? _hands.GetActiveItem((remoteEntity, remoteHands))
            : null;
        if (target == remoteEntity && activeItem is { } heldItem)
        {
            if (args.AltInteract)
                _hands.TryUseItemInHand(remoteEntity, true);
            else
                _interaction.UseInHandInteraction(remoteEntity, heldItem);

            RefreshRemoteState(uid, component, remoteEntity);
            return;
        }

        if (target is { } item
            && TryComp<ItemComponent>(item, out var itemComp)
            && itemComp.AllowDirectHandPickup
            && !HasComp<ActivatableUIComponent>(item)
            && TryComp<HandsComponent>(remoteEntity, out var hands)
            && !_hands.TryGetActiveItem((remoteEntity, hands), out _)
            && _hands.TryPickupAnyHand(remoteEntity, item, checkActionBlocker: false, handsComp: hands, item: itemComp))
        {
            RefreshRemoteState(uid, component, remoteEntity);
            return;
        }

        if (!args.AltInteract
            && target is { } attackTarget
            && TryComp<CombatModeComponent>(remoteEntity, out var combatMode)
            && combatMode.IsInCombatMode
            && !HasComp<MapGridComponent>(attackTarget)
            && !HasComp<MapComponent>(attackTarget)
            && EntityManager.System<SharedMeleeWeaponSystem>().TryGetWeapon(remoteEntity, out var weaponUid, out var weapon))
        {
            EntityManager.System<SharedMeleeWeaponSystem>().AttemptLightAttack(remoteEntity, weaponUid, weapon, attackTarget);
            RefreshRemoteState(uid, component, remoteEntity);
            return;
        }

        if (args.PipeLayer is { } pipeLayer
            && activeItem is { } rcdItem
            && TryComp<RCDComponent>(rcdItem, out var rcd)
            && (rcd.IsRpd || rcd.IsRPLD)
            && rcd.CurrentMode == RpdMode.Free)
        {
            var selectedLayer = rcd.IsRPLD && pipeLayer > AtmosPipeLayer.Tertiary
                ? AtmosPipeLayer.Tertiary
                : pipeLayer;
            EntityManager.System<RCDSystem>().SetSelectedLayer((rcdItem, rcd), selectedLayer);
        }

        _interaction.UserInteraction(remoteEntity, GetCoordinates(args.Coordinates), target, args.AltInteract);
        RefreshRemoteState(uid, component, remoteEntity);
    }

    private async void OnRemoteBuildConstruction(EntityUid uid, RemoteControlConsoleComponent component,
        RemoteControlBuildConstructionMessage args)
    {
        if (!TryGetControlledEntity(component, args.Actor, out var remoteEntity)
            || !_playerManager.TryGetSessionByEntity(args.Actor, out var session))
            return;

        var construction = EntityManager.System<ConstructionSystem>();
        await construction.TryStartStructureConstruction(
            new TryStartStructureConstructionMessage(args.Location, args.PrototypeName, args.Angle, args.Ack),
            remoteEntity,
            session);
    }

    private async void OnRemoteBuildItemConstruction(EntityUid uid, RemoteControlConsoleComponent component,
        RemoteControlBuildItemConstructionMessage args)
    {
        if (!TryGetControlledEntity(component, args.Actor, out var remoteEntity))
            return;

        if (await EntityManager.System<ConstructionSystem>().TryStartItemConstruction(args.PrototypeName, remoteEntity))
            RefreshRemoteState(uid, component, remoteEntity);
    }

    private void CycleRemoteActiveHand(Entity<HandsComponent?> remote)
        => _hands.TryCycleActiveHand(remote);

    private void OnRemoteHand(EntityUid uid, RemoteControlConsoleComponent component, RemoteControlHandMessage args)
    {
        if (!TryGetControlledEntity(component, args.Actor, out var remoteEntity))
            return;

        switch (args.Action)
        {
            case RemoteControlHandAction.SetActive:
                _hands.TrySetActiveHand(new Entity<HandsComponent?>(remoteEntity, null), args.Hand);
                break;
            case RemoteControlHandAction.Use:
                _hands.TryUseItemInHand(remoteEntity, handName: args.Hand);
                break;
            case RemoteControlHandAction.Activate:
                _hands.TryActivateItemInHand(remoteEntity, handName: args.Hand);
                break;
            case RemoteControlHandAction.AltUse:
                _hands.TryUseItemInHand(remoteEntity, true, handName: args.Hand);
                break;
            case RemoteControlHandAction.MoveToActive:
                _hands.TryMoveHeldEntityToActiveHand(remoteEntity, args.Hand);
                break;
            case RemoteControlHandAction.Drop:
                _hands.TryDrop(remoteEntity, args.Hand);
                break;
            case RemoteControlHandAction.CycleActive:
                CycleRemoteActiveHand(remoteEntity);
                break;
            case RemoteControlHandAction.InteractWithHand:
                _hands.TryInteractHandWithActiveHand(remoteEntity, args.Hand);
                break;
        }

        RefreshRemoteState(uid, component, remoteEntity);
    }

    private void OnRemoteInventory(EntityUid uid, RemoteControlConsoleComponent component, RemoteControlInventoryMessage args)
    {
        if (!TryGetControlledEntity(component, args.Actor, out var remoteEntity)
            || !TryComp<InventoryComponent>(remoteEntity, out var inventory))
            return;

        switch (args.Action)
        {
            case RemoteControlInventoryAction.UseSlot:
                UseRemoteSlot(remoteEntity, args.Slot, inventory);
                break;
            case RemoteControlInventoryAction.OpenStorage:
                if (_inventory.TryGetSlotEntity(remoteEntity, args.Slot, out var item, inventory)
                    && item is { } storageItem
                    && TryComp<StorageComponent>(storageItem, out var storage))
                    _storage.OpenStorageUI(storageItem, args.Actor, storage, false);
                break;
        }

        RefreshRemoteState(uid, component, remoteEntity);
    }

    private void UseRemoteSlot(EntityUid remoteEntity, string slot, InventoryComponent inventory)
    {
        if (!TryComp<HandsComponent>(remoteEntity, out var hands))
            return;

        var held = _hands.GetActiveItem((remoteEntity, hands));
        _inventory.TryGetSlotEntity(remoteEntity, slot, out var item, inventory);

        if (held is { } heldItem && item is { } slotItem)
        {
            _interaction.InteractUsing(remoteEntity, heldItem, slotItem, Transform(slotItem).Coordinates);
            return;
        }

        if (item is { } equipped)
        {
            if (!_inventory.TryUnequip(remoteEntity, slot, out var removed, predicted: true,
                    inventory: inventory, checkDoafter: true, triggerHandContact: true))
                return;

            _hands.PickupOrDrop(remoteEntity, removed.Value);
            return;
        }

        if (held is { } heldEntity)
            _inventory.TryEquip(remoteEntity, heldEntity, slot, predicted: true, inventory: inventory,
                force: true, checkDoafter: true, triggerHandContact: true);
    }

    private void OnRemoteStorage(EntityUid uid, RemoteControlConsoleComponent component, RemoteControlStorageMessage args)
    {
        if (!TryGetControlledEntity(component, args.Actor, out var remoteEntity)
            || !TryGetEntity(args.Item, out var item)
            || !TryGetEntity(args.Storage, out var storage)
            || !TryComp<ItemComponent>(item, out var itemComp)
            || !TryComp<StorageComponent>(storage, out var storageComp))
            return;

        switch (args.Action)
        {
            case RemoteControlStorageAction.InteractWithItem:
                if (_hands.TryGetActiveItem(remoteEntity, out _))
                {
                    _interaction.InteractUsing(remoteEntity, _hands.GetActiveItem(remoteEntity)!.Value,
                        item.Value, Transform(item.Value).Coordinates, checkCanInteract: false);
                }
                else
                {
                    _hands.TryPickupAnyHand(remoteEntity, item.Value);
                }
                break;
            case RemoteControlStorageAction.SetItemLocation:
                if (_storage.TryGetStorageLocation((item.Value, itemComp), out var sourceContainer, out _, out _)
                    && sourceContainer.Owner == storage.Value
                    && storageComp.StoredItems.ContainsKey(item.Value))
                {
                    _storage.TrySetItemStorageLocation((item.Value, itemComp), (storage.Value, storageComp), args.Location);
                }
                break;
            case RemoteControlStorageAction.TransferItem:
                if (args.TargetStorage is not { } targetNet
                    || !TryGetEntity(targetNet, out var targetStorage)
                    || !TryComp<StorageComponent>(targetStorage, out var targetComp)
                    || !_storage.TryGetStorageLocation((item.Value, itemComp), out var source, out _, out _)
                    || source.Owner != storage.Value
                    || !_hands.TryPickup(remoteEntity, item.Value, item: itemComp, animate: false))
                    break;

                if (_storage.ItemFitsInGridLocation((item.Value, itemComp), (targetStorage.Value, targetComp), args.Location))
                    _storage.InsertAt((targetStorage.Value, targetComp), (item.Value, itemComp), args.Location, out _, remoteEntity,
                        stackAutomatically: false);
                break;
            case RemoteControlStorageAction.InsertItem:
                if (!TryComp<HandsComponent>(remoteEntity, out var remoteHands)
                    || !_hands.IsHolding((remoteEntity, remoteHands), item)
                    || !_storage.ItemFitsInGridLocation((item.Value, itemComp), (storage.Value, storageComp), args.Location))
                    break;

                _storage.InsertAt((storage.Value, storageComp), (item.Value, itemComp), args.Location, out _, remoteEntity,
                    stackAutomatically: false);
                break;
            case RemoteControlStorageAction.SaveItemLocation:
                if (storageComp.StoredItems.ContainsKey(item.Value))
                    _storage.SaveItemLocation((storage.Value, storageComp), (item.Value, MetaData(item.Value)));
                break;
        }

        RefreshRemoteState(uid, component, remoteEntity);
    }

    private bool TryGetControlledEntity(RemoteControlConsoleComponent component, EntityUid actor,
        out EntityUid remoteEntity)
    {
        remoteEntity = default;
        return component.Controller == actor && TryGetRemoteEntity(component, out remoteEntity);
    }

    public bool TryGetControlledEntity(EntityUid actor, out EntityUid remoteEntity)
    {
        foreach (var console in EntityQuery<RemoteControlConsoleComponent>())
        {
            if (console.Controller == actor && TryGetRemoteEntity(console, out remoteEntity))
                return true;
        }

        remoteEntity = default;
        return false;
    }

    [SubscribeLocalEvent]
    private void OnRemoteBorgTypeAction(BorgToggleSelectTypeEvent args)
    {
        if (TryOpenRemoteUi<BorgSwitchableTypeComponent>(args.Performer, BorgSwitchableTypeUiKey.SelectBorgType))
            args.Handled = true;
    }

    private bool TryOpenRemoteUi<TComponent>(EntityUid remoteEntity, Enum uiKey) where TComponent : Component
    {
        if (!TryFindConsole(remoteEntity, out _, out var controller)
            || !TryComp<TComponent>(remoteEntity, out _)
            || !_playerManager.TryGetSessionByEntity(controller, out var session))
            return false;

        _remoteUiRangeOverrides.TryAdd((remoteEntity, uiKey, controller), 0);
        _ui.OpenUi((remoteEntity, null), uiKey, session);
        return true;
    }

    [SubscribeLocalEvent]
    private void OnRemoteUiOpened(Entity<UserInterfaceComponent> entity, ref BoundUIOpenedEvent args)
    {
        if (!TryFindConsole(args.Actor, out var console, out var controller))
            return;

        if (args.Actor == controller)
            return;

        if (!_playerManager.TryGetSessionByEntity(controller, out var controllerSession))
            return;

        if (HasComp<QuickConstructableComponent>(entity.Owner))
            return;

        _remoteUiRangeOverrides.TryAdd((entity.Owner, args.UiKey, args.Actor), 0);
        _pendingRemoteUiMirrors.Add((entity.Owner, args.UiKey, controller, console.Owner));
    }

    [SubscribeLocalEvent]
    private void OnRemoteUiClosed(Entity<UserInterfaceComponent> entity, ref BoundUIClosedEvent args)
    {
        _remoteUiRangeOverrides.TryRemove((entity.Owner, args.UiKey, args.Actor), out _);

        if (HasComp<QuickConstructableComponent>(entity.Owner))
            return;

        if (TryFindConsole(args.Actor, out _, out var controller))
        {
            _ui.CloseUi((entity.Owner, null), args.UiKey, controller);
            return;
        }

        if (TryGetControlledEntity(args.Actor, out var remoteEntity))
        {
            _ui.CloseUi((entity.Owner, null), args.UiKey, remoteEntity);
        }
    }
    [SubscribeLocalEvent]
    private void OnRemoteUiCheckRange(Entity<UserInterfaceComponent> entity, ref BoundUserInterfaceCheckRangeEvent args)
    {
        if (args.UiKey is RemoteControlUIKey)
            return;

        if (_remoteUiRangeOverrides.ContainsKey((entity.Owner, args.UiKey, args.Actor.Owner)))
            args.Result = BoundUserInterfaceRangeResult.Pass;

        if (args.Result == BoundUserInterfaceRangeResult.Default
            && TryGetControlledEntity(args.Actor.Owner, out var remoteEntity))
        {
            args.Result = _interaction.InRangeUnobstructed(remoteEntity, entity.Owner, args.Data.InteractionRange)
                ? BoundUserInterfaceRangeResult.Pass
                : BoundUserInterfaceRangeResult.Fail;
        }
    }

    private bool TryFindConsole(EntityUid remoteEntity, out Entity<RemoteControlConsoleComponent> console, out EntityUid controller)
    {
        foreach (var candidate in EntityQuery<RemoteControlConsoleComponent>())
        {
            if (!TryGetRemoteEntity(candidate, out var candidateRemote)
                || candidateRemote != remoteEntity
                || candidate.Controller is not { } activeController)
                continue;

            console = (candidate.Owner, candidate);
            controller = activeController;
            return true;
        }

        console = default;
        controller = default;
        return false;
    }

    private void OnUiClosed(EntityUid uid, RemoteControlConsoleComponent component, BoundUIClosedEvent args)
    {
        _sawmill.Debug($"[RemoteControl] server UI close console={uid} actor={args.Actor} usersBefore={string.Join(',', component.Users)} controller={component.Controller}");

        if (!args.Actor.Valid)
            return;

        if (!component.Users.Remove(args.Actor))
            return;

        if (component.Controller == args.Actor)
            SetController(uid, component, null, TryGetRemoteEntity(component, out var controlledEntity) ? controlledEntity : null);

        if (TryGetRemoteEntity(component, out var remoteEntity)
            && _playerManager.TryGetSessionByEntity(args.Actor, out var session))
        {
            RemoveRemotePvsOverrides(remoteEntity, args.Actor, session);
            _viewSubscriber.RemoveViewSubscriber(remoteEntity, session);
        }

        if (!TryComp<RelayInputMoverComponent>(args.Actor, out var relay))
            return;

        if (!TryGetBody(component, out var body) || relay.RelayEntity == body)
            RemComp(args.Actor, relay);

    }

    [SubscribeLocalEvent]
    private void OnPortDisconnected(Entity<RemoteControlConsoleComponent> entity, ref PortDisconnectedEvent args)
    {
        if (args.Source != entity.Owner || entity.Comp.RemoteBrain != args.Sink || args.Port != "RemoteControl")
            return;

        if (TryGetRemoteEntity(entity.Comp, out var remoteEntity))
        {
            CleanupUsers(entity, remoteEntity);
        }

        if (TryGetBorg(args.Sink, out var chassis))
            DeactivateRemoteBorg(entity.Comp, chassis);

        entity.Comp.RemoteBrain = null;
        Dirty(entity);
    }

    private bool TryGetBorg(EntityUid brain, out EntityUid chassis)
    {
        chassis = default;
        return _container.TryGetContainingContainer(brain, out var container)
            && TryComp<BorgChassisComponent>(container.Owner, out _)
            && (chassis = container.Owner) != default;
    }

    private void DeactivateRemoteBorg(RemoteControlConsoleComponent console, EntityUid chassis)
    {
        if (!console.BorgActivatedByRemote
            || !TryComp<BorgChassisComponent>(chassis, out var chassisComp))
            return;

        _borg.SetActive((chassis, chassisComp), false);
        console.BorgActivatedByRemote = false;
    }

    private bool TryActivateRemoteBorg(RemoteControlConsoleComponent console, EntityUid chassis)
    {
        if (!TryComp<BorgChassisComponent>(chassis, out var chassisComp) || chassisComp.Active)
            return chassisComp?.Active ?? false;

        if (!_borg.TryActivate((chassis, chassisComp), allowRemoteControl: true))
        {
            return chassisComp.Active;
        }

        console.BorgActivatedByRemote = true;
        return true;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        foreach (var console in EntityQuery<RemoteControlConsoleComponent>())
        {
            EntityUid? disconnectedUser = null;
            foreach (var user in console.Users)
            {
                if (!_playerManager.TryGetSessionByEntity(user, out var session) || session != args.Session)
                    continue;

                if (TryGetRemoteEntity(console, out var remoteEntity))
                {
                    if (console.Controller == user)
                        SetController(console.Owner, console, null, remoteEntity);

                    RemoveRemotePvsOverrides(remoteEntity, user, session);
                    _viewSubscriber.RemoveViewSubscriber(remoteEntity, session);
                }

                disconnectedUser = user;
                break;
            }

            if (disconnectedUser is { } userToRemove)
                console.Users.Remove(userToRemove);
        }
    }

    [SubscribeLocalEvent]
    private void OnMobStateChanged(MobStateChangedEvent args)
        => ReleaseControlIfInvalid(args.Target);

    [SubscribeLocalEvent]
    private void OnSleepStateChanged(Entity<SleepingComponent> ent, ref SleepStateChangedEvent args)
    {
        if (args.FellAsleep)
            ReleaseControlIfInvalid(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var (uiEntity, uiKey, controller, console) in _pendingRemoteUiMirrors)
        {
            if (!_playerManager.TryGetSessionByEntity(controller, out _))
                continue;

            _ui.OpenUi((uiEntity, null), uiKey, controller);

            if (TryComp<UserInterfaceComponent>(uiEntity, out var uiComponent))
                Dirty(uiEntity, uiComponent);
        }

        _pendingRemoteUiMirrors.Clear();

        foreach (var chassis in _pendingBorgRefreshes)
            RefreshRemoteBorgModuleState(chassis);

        _pendingBorgRefreshes.Clear();

        foreach (var console in EntityQuery<RemoteControlConsoleComponent>())
        {
            if (console.Controller is not { } controller
                || _actionBlocker.CanConsciouslyPerformAction(controller)
                || !TryGetRemoteEntity(console, out var remoteEntity))
                continue;

            SetController(console.Owner, console, null, remoteEntity);
        }
    }

    private void ReleaseControlIfInvalid(EntityUid user)
    {
        foreach (var console in EntityQuery<RemoteControlConsoleComponent>())
        {
            if (console.Controller != user || !TryGetRemoteEntity(console, out var remoteEntity))
                continue;

            SetController(console.Owner, console, null, remoteEntity);
        }
    }

    [SubscribeLocalEvent]
    private void OnConsoleShutdown(Entity<RemoteControlConsoleComponent> entity, ref ComponentShutdown args)
    {
        if (!TryGetRemoteEntity(entity.Comp, out var remoteEntity))
            return;

        CleanupUsers(entity, remoteEntity);
    }

    private void CleanupUsers(Entity<RemoteControlConsoleComponent> entity, EntityUid remoteEntity)
    {
        DeactivateRemoteBorg(entity.Comp, remoteEntity);
        entity.Comp.Controller = null;
        foreach (var user in entity.Comp.Users)
        {
            RemovePvsOverride(remoteEntity, user);

            if (TryComp<RelayInputMoverComponent>(user, out var relay)
                && relay.RelayEntity == remoteEntity)
            {
                RemComp(user, relay);
            }
        }

        entity.Comp.Users.Clear();
    }

    private void SetController(EntityUid consoleUid, RemoteControlConsoleComponent component, EntityUid? controller,
        EntityUid? remoteEntity)
    {
        _sawmill.Debug($"[RemoteControl] server controller console={consoleUid} old={component.Controller} new={controller} remote={remoteEntity}");

        if (component.Controller is { } oldController && TryComp<RelayInputMoverComponent>(oldController, out var oldRelay))
            RemComp(oldController, oldRelay);

        if (controller is null && component.Controller is not null && remoteEntity is { } chassis)
            DeactivateRemoteBorg(component, chassis);
        else if (controller is not null && component.Controller != controller && remoteEntity is { } chassisToActivate)
            TryActivateRemoteBorg(component, chassisToActivate);

        component.Controller = controller;

        if (controller is { } newController && TryGetBody(component, out var body))
        {
            _mover.SetRelay(newController, body);
        }

        if (remoteEntity is { } refreshEntity)
            RefreshRemoteState(consoleUid, component, refreshEntity);
    }

    private void RefreshRemoteState(EntityUid consoleUid, RemoteControlConsoleComponent component, EntityUid remoteEntity)
    {
        foreach (var user in component.Users)
        {
            if (_playerManager.TryGetSessionByEntity(user, out var session))
                AddRemotePvsOverrides(remoteEntity, user, session);
        }

        var actionEntities = GetRemoteActionEntities(remoteEntity);
        var quickConstructionItem = GetRemoteQuickConstructionItem(remoteEntity);
        _ui.SetUiState(consoleUid, RemoteControlUIKey.Key, new RemoteControlConsoleBuiState
        {
            Connected = true,
            RemoteEntity = GetNetEntity(remoteEntity),
            Controller = component.Controller is { } controllerUser ? GetNetEntity(controllerUser) : null,
            Actions = GetRemoteActions(actionEntities),
            QuickConstructionItem = quickConstructionItem is { } item ? GetNetEntity(item) : null,
            Hands = GetRemoteHands(remoteEntity),
            Inventory = GetRemoteInventory(remoteEntity),
            SelectedAction = GetSelectedRemoteAction(remoteEntity),
        });
    }

    private EntityUid? GetRemoteQuickConstructionItem(EntityUid remoteEntity)
    {
        if (!TryComp<HandsComponent>(remoteEntity, out var hands))
            return null;

        var activeItem = _hands.GetActiveItem((remoteEntity, hands));
        return activeItem is { } active
            && HasComp<QuickConstructableComponent>(active)
            && _ui.IsUiOpen(active, QuickConstructionUiKey.Key, remoteEntity)
            ? active
            : null;
    }

    private NetEntity? GetSelectedRemoteAction(EntityUid remoteEntity)
    {
        if (!TryComp<BorgChassisComponent>(remoteEntity, out var chassis)
            || chassis.SelectedModule is not { } module
            || !TryComp<SelectableBorgModuleComponent>(module, out var selectable)
            || selectable.ModuleSwapActionEntity is not { } action)
            return null;

        return GetNetEntity(action);
    }

    private RemoteControlHandState[] GetRemoteHands(EntityUid remoteEntity)
    {
        if (!TryComp<HandsComponent>(remoteEntity, out var hands))
            return System.Array.Empty<RemoteControlHandState>();

        var result = new List<RemoteControlHandState>(hands.SortedHands.Count);
        foreach (var hand in hands.SortedHands)
        {
            _hands.TryGetHeldItem((remoteEntity, hands), hand, out var item);
            result.Add(new RemoteControlHandState
            {
                Name = hand,
                Location = hands.Hands[hand].Location,
                EmptyRepresentative = hands.Hands[hand].EmptyRepresentative,
                HeldItem = item is { } held ? GetNetEntity(held) : null,
                Active = hands.ActiveHandId == hand,
            });
        }

        return result.ToArray();
    }

    private RemoteControlInventorySlotState[] GetRemoteInventory(EntityUid remoteEntity)
    {
        if (!TryComp<InventoryComponent>(remoteEntity, out var inventory))
            return System.Array.Empty<RemoteControlInventorySlotState>();

        var result = new List<RemoteControlInventorySlotState>(inventory.Slots.Length);
        foreach (var slot in inventory.Slots)
        {
            _inventory.TryGetSlotEntity(remoteEntity, slot.Name, out var item, inventory);
            result.Add(new RemoteControlInventorySlotState
            {
                Name = slot.Name,
                Group = slot.SlotGroup,
                TextureName = "Slots/" + slot.TextureName,
                FullTextureName = slot.FullTextureName,
                Item = item is { } equipped ? GetNetEntity(equipped) : null,
                HasStorage = item is { } storageItem && HasComp<StorageComponent>(storageItem),
            });
        }

        return result.ToArray();
    }

    private NetEntity[] GetRemoteActions(HashSet<EntityUid> actionEntities)
    {
        var result = new NetEntity[actionEntities.Count];
        var index = 0;
        foreach (var action in actionEntities.OrderBy(action => action.Id))
            result[index++] = GetNetEntity(action);

        return result;
    }

    private HashSet<EntityUid> GetRemoteActionEntities(EntityUid remoteEntity)
    {
        var result = new HashSet<EntityUid>();
        if (TryComp<ActionsComponent>(remoteEntity, out var actions))
        {
            foreach (var action in _actions.GetActions(remoteEntity, actions))
                result.Add(action.Owner);
        }

        if (!TryComp<BorgChassisComponent>(remoteEntity, out var chassis))
            return result;

        foreach (var module in chassis.ModuleContainer.ContainedEntities)
        {
            if (TryComp<SelectableBorgModuleComponent>(module, out var selectable)
                && selectable.ModuleSwapActionEntity is { } action
                && Exists(action))
            {
                result.Add(action);
            }
        }

        return result;
    }

    private void AddRemotePvsOverrides(EntityUid remoteEntity, EntityUid user, ICommonSession session)
    {
        _pvsOverride.AddSessionOverride(remoteEntity, session);

        if (_remoteActionOverrides.TryGetValue(user, out var previousActions))
        {
            foreach (var action in previousActions)
                _pvsOverride.RemoveSessionOverride(action, session);

            previousActions.Clear();
        }

        if (!_remoteActionOverrides.TryGetValue(user, out var actionOverrides))
        {
            actionOverrides = new HashSet<EntityUid>();
            _remoteActionOverrides[user] = actionOverrides;
        }

        if (TryComp<BorgChassisComponent>(remoteEntity, out var chassis))
        {
            foreach (var module in chassis.ModuleContainer.ContainedEntities)
            {
                _pvsOverride.AddSessionOverride(module, session);
                actionOverrides.Add(module);
            }
        }

        if (TryComp<HandsComponent>(remoteEntity, out var hands))
        {
            foreach (var hand in hands.SortedHands)
            {
                if (!_hands.TryGetHeldItem((remoteEntity, hands), hand, out var held))
                    continue;

                _pvsOverride.AddSessionOverride(held.Value, session);
                actionOverrides.Add(held.Value);
            }
        }

        foreach (var action in GetRemoteActionEntities(remoteEntity))
        {
            _pvsOverride.AddSessionOverride(action, session);
            actionOverrides.Add(action);
        }
    }

    private void RemoveRemotePvsOverrides(EntityUid remoteEntity, EntityUid user, ICommonSession session)
    {
        _pvsOverride.RemoveSessionOverride(remoteEntity, session);

        if (_remoteActionOverrides.Remove(user, out var actionOverrides))
        {
            foreach (var action in actionOverrides)
                _pvsOverride.RemoveSessionOverride(action, session);
        }
    }

    private void RemovePvsOverride(EntityUid remoteEntity, EntityUid user)
    {
        if (_playerManager.TryGetSessionByEntity(user, out var session))
        {
            RemoveRemotePvsOverrides(remoteEntity, user, session);
            _viewSubscriber.RemoveViewSubscriber(remoteEntity, session);
        }
    }

    private bool TryGetBody(RemoteControlConsoleComponent component, out EntityUid body)
    {
        body = default;
        if (component.RemoteBrain is not { } brain)
            return false;

        if (TryComp<OrganComponent>(brain, out var organ) && organ.Body is { } organBody)
        {
            body = organBody;
            return true;
        }

        return _container.TryGetContainingContainer(brain, out var container)
            && container.ID == "borg_brain"
            && TryComp<BorgChassisComponent>(container.Owner, out _)
            && (body = container.Owner) != default;
    }

    private bool TryGetRemoteEntity(RemoteControlConsoleComponent component, out EntityUid remoteEntity)
    {
        remoteEntity = default;
        if (component.RemoteBrain is not { } brain)
            return false;

        if (TryGetBody(component, out var body))
        {
            remoteEntity = body;
            return true;
        }

        remoteEntity = brain;
        return true;
    }
}
