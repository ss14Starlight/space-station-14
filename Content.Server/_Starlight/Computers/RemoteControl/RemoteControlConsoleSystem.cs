using System.Collections.Concurrent;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Construction;
using Content.Server.Movement.Systems;
using Content.Server.Popups;
using Content.Shared._Afterlight.Silicons.Borgs;
using Content.Shared._DEN.QuickConstruction.Components;
using Content.Shared._DEN.QuickConstruction.Events;
using Content.Shared._Starlight.Chat;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.Silicons;
using Content.Shared._Starlight.Silicons.Borgs;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Organ;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.CombatMode;
using Content.Shared.Construction;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell.Components;
using Content.Shared.RCD.Components;
using Content.Shared.RCD.Systems;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using static Content.Server.Chat.Systems.ChatSystem;
// Starlight

namespace Content.Server._Starlight.Computers.RemoteControl;

public sealed partial class RemoteControlConsoleSystem : EntitySystem
{
    private readonly HashSet<EntityUid> _pendingBorgRefreshes = new();
    private readonly HashSet<(EntityUid Console, EntityUid Chassis)> _pendingRemoteBorgActivations = new();
    private readonly HashSet<(EntityUid UiEntity, Enum UiKey, EntityUid Controller)> _pendingRemoteUiMirrors = new();
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _remoteActionOverrides = new();

    private readonly Dictionary<EntityUid, (EntityUid Storage, EntityUid Controller, EntityUid Remote)>
        _remoteStorageUiActors = new();

    private readonly ConcurrentDictionary<(EntityUid UiEntity, Enum UiKey, EntityUid Actor), byte>
        _remoteUiRangeOverrides = new();

    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ActivatableUISystem _activatableUi = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SharedBorgSystem _borg = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private PullController _pullController = default!;
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;

    [Dependency] private SharedStorageSystem _storage = default!;

    // Dependency fields not read only. Ignore IDE0044.
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedViewSubscriberSystem _viewSubscriber = default!;

    public override void Initialize()
    {
        SubscribeAllEvent<TypingChangedEvent>(OnRemoteControllerTypingChanged);
        Subs.BuiEvents<RemoteControlConsoleComponent>(RemoteControlUIKey.Key,
            subs =>
            {
                subs.Event<BoundUIClosedEvent>(OnUiClosed);
                subs.Event<RemoteControlToggleMessage>(OnToggleControl);
                subs.Event<RemoteControlInteractionMessage>(OnRemoteInteraction);
                subs.Event<RemoteControlBuildConstructionMessage>(OnRemoteBuildConstruction);
                subs.Event<RemoteControlBuildItemConstructionMessage>(OnRemoteBuildItemConstruction);
                subs.Event<RemoteControlActionMessage>(OnRemoteAction);
                subs.Event<RemoteControlTargetActionMessage>(OnRemoteTargetAction);
                subs.Event<RemoteControlHandMessage>(OnRemoteHand);
                subs.Event<RemoteControlInventoryMessage>(OnRemoteInventory);
            });
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        _hands.OnHandSetActive += OnHandSetActive;
    }

    [SubscribeLocalEvent]
    private void OnRemotePowerCellInserted(Entity<PowerCellComponent> cell, ref EntGotInsertedIntoContainerMessage args)
    {
        if (!TryComp<PowerCellSlotComponent>(args.Container.Owner, out var slot)
            || args.Container.ID != slot.CellSlotId
            || !HasComp<BorgChassisComponent>(args.Container.Owner))
            return;

        QueueRemoteBorgActivation(args.Container.Owner);
    }

    [SubscribeLocalEvent]
    private void OnRemoteBorgBatteryStateChanged(Entity<BatteryComponent> battery, ref BatteryStateChangedEvent args)
    {
        if (!TryGetPowerCellBorg(battery.Owner, out var chassis))
            return;

        if (args.NewState == BatteryState.Empty)
        {
            RemovePendingRemoteBorgActivations(chassis);
            return;
        }

        QueueRemoteBorgActivation(chassis);
    }

    private bool TryGetPowerCellBorg(EntityUid cell, out EntityUid chassis)
    {
        chassis = default;
        return _container.TryGetContainingContainer(cell, out var container)
                && TryComp<PowerCellSlotComponent>(container.Owner, out var slot)
                && container.ID == slot.CellSlotId
                && TryComp<BorgChassisComponent>(container.Owner, out _)
                && (chassis = container.Owner) != default;
    }

    private void QueueRemoteBorgActivation(EntityUid chassis)
    {
        foreach (var console in EntityQuery<RemoteControlConsoleComponent>())
            if (console.Controller is not null
                && TryGetRemoteEntity(console, out var remoteEntity)
                && remoteEntity == chassis)
                _pendingRemoteBorgActivations.Add((console.Owner, chassis));
    }

    private void RemovePendingRemoteBorgActivations(EntityUid chassis)
        => _pendingRemoteBorgActivations.RemoveWhere(pending => pending.Chassis == chassis);

    private void UpdatePendingRemoteBorgActivations()
    {
        foreach (var pending in _pendingRemoteBorgActivations.ToArray())
        {
            if (!TryComp<RemoteControlConsoleComponent>(pending.Console, out var console)
                || console.Controller is null
                || !TryGetRemoteEntity(console, out var remoteEntity)
                || remoteEntity != pending.Chassis
                || !TryComp<BorgChassisComponent>(pending.Chassis, out var chassis))
            {
                _pendingRemoteBorgActivations.Remove(pending);
                continue;
            }

            if (chassis.Active || TryActivateRemoteBorg(console, pending.Chassis))
                _pendingRemoteBorgActivations.Remove(pending);
        }
    }

    [SubscribeLocalEvent]
    private void OnRemoteInventoryChanged(RemoteControlInventoryChangedEvent args)
        => RefreshRemoteConsoleStates(args.Actor);

    [SubscribeLocalEvent]
    private void OnRemoteControlInteractionCheck(ref RemoteControlInteractionCheckEvent args)
    {
        if (!TryGetControlledEntity(args.Actor, out var remoteEntity))
            return;

        args.RemoteEntity = remoteEntity;
        args.Allowed = _interaction.InRangeAndAccessible(remoteEntity, args.Target);
    }

    [SubscribeLocalEvent]
    private void OnToyRemoteDropped(Entity<RemoteControlConsoleComponent> entity, ref DroppedEvent args)
    {
        if (entity.Comp.EnableRemoteView
            || !TryComp<ItemToggleComponent>(entity.Owner, out var toggle)
            || !toggle.Activated)
            return;

        _itemToggle.TryDeactivate((entity.Owner, toggle), args.User, false, false);
    }

    [SubscribeLocalEvent]
    private void OnToyRemoteActivateAttempt(Entity<RemoteControlConsoleComponent> entity,
        ref ItemToggleActivateAttemptEvent args)
    {
        if (entity.Comp.EnableRemoteView)
            return;

        if (args.User is not { } user
            || entity.Comp.Controller is not null
            || !_actionBlocker.CanConsciouslyPerformAction(user)
            || !TryGetRemoteEntity(entity.Comp, out var remoteEntity))
        {
            args.Cancelled = true;
            return;
        }

        if (!CanRemoteControlTarget(entity.Owner, entity.Comp, remoteEntity, user))
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnToyRemoteToggled(Entity<RemoteControlConsoleComponent> entity, ref ItemToggledEvent args)
    {
        if (entity.Comp.EnableRemoteView)
            return;

        if (args.Activated)
        {
            if (args.User is not { } controller
                || entity.Comp.Controller is not null
                || !TryGetRemoteEntity(entity.Comp, out var remoteEntity))
                return;

            entity.Comp.Users.Add(controller);
            if (!SetController(entity.Owner, entity.Comp, controller, remoteEntity, false))
            {
                entity.Comp.Users.Remove(controller);
                DeactivateToyRemote(entity.Owner, entity.Comp);
            }

            return;
        }

        if (entity.Comp.Controller is not { } activeController
            || !TryGetRemoteEntity(entity.Comp, out var controlledEntity))
            return;

        SetController(entity.Owner, entity.Comp, null, controlledEntity, false);
        entity.Comp.Users.Remove(activeController);
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

            if (_hands.GetActiveItem((hands.Owner, hands.Comp)) is { } activeItem
                && HasComp<QuickConstructableComponent>(activeItem)
                && _ui.IsUiOpen(activeItem, QuickConstructionUiKey.Key, hands.Owner))
                _ui.CloseUi(activeItem, QuickConstructionUiKey.Key, hands.Owner);

            RefreshRemoteState(consoleUid, console, remoteEntity);
        }
    }

    private void RefreshRemoteContainerState(EntityUid containerOwner)
        => RefreshRemoteConsoleStates(containerOwner);

    [SubscribeLocalEvent]
    private void OnRemoteContainerInserted(EntGotInsertedIntoContainerMessage args)
        => RefreshRemoteContainerState(args.Container.Owner);

    [SubscribeLocalEvent]
    private void OnRemoteContainerRemoved(EntGotRemovedFromContainerMessage args)
        => RefreshRemoteContainerState(args.Container.Owner);

    [SubscribeLocalEvent]
    private void OnRemoteBorgModuleSelected(Entity<SelectableBorgModuleComponent> module,
        ref BorgModuleSelectedEvent args)
        => _pendingBorgRefreshes.Add(args.Chassis);

    [SubscribeLocalEvent]
    private void OnRemoteBorgModuleUnselected(Entity<SelectableBorgModuleComponent> module,
        ref BorgModuleUnselectedEvent args)
        => _pendingBorgRefreshes.Add(args.Chassis);

    [SubscribeLocalEvent]
    private void OnRemoteBorgModuleInstalled(Entity<BorgModuleComponent> module, ref BorgModuleInstalledEvent args)
        => _pendingBorgRefreshes.Add(args.ChassisEnt);

    [SubscribeLocalEvent]
    private void OnRemoteBorgModuleUninstalled(Entity<BorgModuleComponent> module, ref BorgModuleUninstalledEvent args)
        => _pendingBorgRefreshes.Add(args.ChassisEnt);

    [SubscribeLocalEvent]
    private void OnRemoteBorgTypeSelected(Entity<BorgSwitchableTypeComponent> borg, ref AfterBorgTypeSelectEvent args)
        => _pendingBorgRefreshes.Add(borg.Owner);

    [SubscribeLocalEvent]
    private void OnRemoteBrainRemoved(Entity<RemoteControlBrainComponent> brain,
        ref EntGotRemovedFromContainerMessage args)
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
            if (console.Controller is { } controller)
                CloseMirroredStorageUis(consoleUid, controller);

            if (!TerminatingOrDeleted(args.Container.Owner))
                SetRemoteTypingState(args.Container.Owner, TypingIndicatorState.None);
            CleanupUsers(consoleEntity, args.Container.Owner, false, false);

            foreach (var user in console.Users)
                if (_playerManager.TryGetSessionByEntity(user, out var session))
                    _viewSubscriber.AddViewSubscriber(brain.Owner, session);

            RefreshRemoteState(consoleUid, console, brain.Owner);
            Dirty(consoleEntity);
        }
    }

    private void RefreshRemoteConsoleStates(EntityUid remoteEntity)
    {
        foreach (var console in EntityQuery<RemoteControlConsoleComponent>())
        {
            if ((console.Users.Count == 0 && console.Controller is null)
                || !TryGetRemoteEntity(console, out var connectedEntity)
                || connectedEntity != remoteEntity)
                continue;

            RefreshRemoteState(console.Owner, console, remoteEntity);
        }
    }

    [SubscribeLocalEvent]
    private void OnNewLink(Entity<RemoteControlConsoleComponent> entity, ref NewLinkEvent args)
    {
        if (args.Source != entity.Owner || !HasComp<RemoteControlBrainComponent>(args.Sink))
            return;

        if (entity.Comp.RemoteBrain is not null && TryGetRemoteEntity(entity.Comp, out var oldRemoteEntity))
            CleanupUsers(entity, oldRemoteEntity, false);

        entity.Comp.RemoteBrain = args.Sink;
        Dirty(entity);

        if (!TryGetRemoteEntity(entity.Comp, out var remoteEntity))
        {
            SetDisconnectedState(entity.Owner);
            return;
        }

        foreach (var user in entity.Comp.Users)
            if (_playerManager.TryGetSessionByEntity(user, out var session))
                _viewSubscriber.AddViewSubscriber(remoteEntity, session);

        RefreshRemoteState(entity.Owner, entity.Comp, remoteEntity);
    }

    [SubscribeLocalEvent]
    private void OnUiOpen(EntityUid uid, RemoteControlConsoleComponent component, BoundUIOpenedEvent args)
    {
        if (!args.UiKey.Equals(RemoteControlUIKey.Key))
            return;

        var user = args.Actor;

        component.Users.Add(user);

        if (!TryGetRemoteEntity(component, out var remoteEntity))
        {
            SetDisconnectedState(uid);
            return;
        }

        if (_playerManager.TryGetSessionByEntity(user, out var session))
        {
            AddRemotePvsOverrides(remoteEntity, user, session);
            _viewSubscriber.AddViewSubscriber(remoteEntity, session);
        }

        if (component.Controller is null && _actionBlocker.CanConsciouslyPerformAction(user))
            SetController(uid, component, user, remoteEntity);

        var actionEntities = component.EnableRemoteView
            ? GetRemoteActionEntities(remoteEntity)
            : new HashSet<EntityUid>();
        var quickConstructionItem = component.EnableRemoteView ? GetRemoteQuickConstructionItem(remoteEntity) : null;

        _ui.SetUiState(uid, RemoteControlUIKey.Key,
            new RemoteControlConsoleBuiState
            {
                Connected = true,
                EnableRemoteView = component.EnableRemoteView,
                RemoteEntity = GetNetEntity(remoteEntity),
                Controller = component.Controller is { } controller ? GetNetEntity(controller) : null,
                Actions = GetRemoteActions(actionEntities),
                SelectedAction = component.EnableRemoteView ? GetSelectedRemoteAction(remoteEntity) : null,
                QuickConstructionItem = quickConstructionItem is { } item ? GetNetEntity(item) : null,
                Hands =
                    component.EnableRemoteView
                        ? GetRemoteHands(remoteEntity)
                        : Array.Empty<RemoteControlHandState>(),
                Inventory = component.EnableRemoteView
                    ? GetRemoteInventory(remoteEntity)
                    : Array.Empty<RemoteControlInventorySlotState>()
            });
    }

    [SubscribeLocalEvent]
    private void OnAIRemoteControl(EntityUid uid, StationAIShuntableComponent shuntable,
        AIRemoteControlActionEvent args)
    {
        if (args.Handled
            || !_actionBlocker.CanConsciouslyPerformAction(uid)
            || !TryComp<RemoteControlConsoleComponent>(uid, out var console)
            || !TryComp<StationAIShuntComponent>(args.Target, out _)
            || !_playerManager.TryGetSessionByEntity(uid, out var session)
            || (console.Controller is { } controller && controller != uid))
            return;

        var remoteBrain = args.Target;
        if (TryComp<BorgChassisComponent>(args.Target, out var chassis))
        {
            if (chassis.BrainContainer.ContainedEntity is not { } brain)
                return;

            remoteBrain = brain;
        }

        var previousBrain = console.RemoteBrain;
        var hasPreviousRemote = TryGetRemoteEntity(console, out var previousRemote);
        console.RemoteBrain = remoteBrain;
        if (hasPreviousRemote
            && TryGetRemoteEntity(console, out var newRemote)
            && previousRemote != newRemote)
        {
            console.RemoteBrain = previousBrain;
            if (console.Controller is not null)
                SetController(uid, console, null, previousRemote, false);

            CleanupUsers((uid, console), previousRemote, false);
            console.RemoteBrain = remoteBrain;
        }

        if (!TryGetRemoteEntity(console, out var remoteEntity))
        {
            console.RemoteBrain = null;
            return;
        }

        if (console.Users.Contains(uid))
        {
            if (console.Controller != uid)
                SetController(uid, console, uid, remoteEntity, false);

            RefreshRemoteState(uid, console, remoteEntity);
        }
        else
            _ui.OpenUi((uid, null), RemoteControlUIKey.Key, session);

        args.Handled = true;
    }

    private void SetDisconnectedState(EntityUid consoleUid)
        => _ui.SetUiState(consoleUid, RemoteControlUIKey.Key,
            new RemoteControlConsoleBuiState
            {
                Connected = false,
                RemoteEntity = null,
                Controller = null,
                Actions = Array.Empty<NetEntity>(),
                SelectedAction = null,
                Hands = Array.Empty<RemoteControlHandState>(),
                Inventory = Array.Empty<RemoteControlInventorySlotState>()
            });

    private void OnToggleControl(EntityUid uid, RemoteControlConsoleComponent component,
        RemoteControlToggleMessage args)
    {
        if (!component.Users.Contains(args.Actor))
            return;

        if (component.Controller == args.Actor)
        {
            SetController(uid, component, null,
                TryGetRemoteEntity(component, out var controlledRemote) ? controlledRemote : null);
            return;
        }

        if (component.Controller != null
            || !_actionBlocker.CanConsciouslyPerformAction(args.Actor)
            || !TryGetRemoteEntity(component, out var remoteEntity))
            return;

        SetController(uid, component, args.Actor, remoteEntity);
    }

    [SubscribeLocalEvent]
    private void OnRemoteEntityPlayerAttached(PlayerAttachedEvent args)
    {
        foreach (var console in EntityQuery<RemoteControlConsoleComponent>())
        {
            if (console.CanForceRemoteControl
                || console.Controller is not { } controller
                || !TryGetRemoteEntity(console, out var remoteEntity)
                || remoteEntity != args.Entity)
                continue;

            _popup.PopupEntity(Loc.GetString("remote-control-control-taken-over"), console.Owner, controller);
            SetController(console.Owner, console, null, remoteEntity);
        }
    }

    private void OnRemoteAction(EntityUid uid, RemoteControlConsoleComponent component, RemoteControlActionMessage args)
    {
        if (component.Controller != args.Actor || !TryGetRemoteEntity(component, out var remoteEntity))
            return;

        var action = GetEntity(args.Action);
        var availableActions = GetRemoteActionEntities(remoteEntity);
        if (!availableActions.Contains(action))
            return;

        var isBorgTypeAction = TryComp<InstantActionComponent>(action, out var instantAction)
                                && instantAction.Event is BorgToggleSelectTypeEvent;
        var hasBorgSwitchableType = TryComp<BorgSwitchableTypeComponent>(remoteEntity, out _);
        var hasControllerSession = _playerManager.TryGetSessionByEntity(args.Actor, out var controllerSession);

        if (isBorgTypeAction && hasBorgSwitchableType && hasControllerSession && controllerSession != null)
        {
            _remoteUiRangeOverrides.TryAdd((remoteEntity, BorgSwitchableTypeUiKey.SelectBorgType, args.Actor), 0);
            _ui.OpenUi((remoteEntity, null), BorgSwitchableTypeUiKey.SelectBorgType, controllerSession!);
            RefreshRemoteState(uid, component, remoteEntity);
            return;
        }

        _actions.TryPerformAction(remoteEntity, action);
    }

    private void OnRemoteTargetAction(EntityUid uid, RemoteControlConsoleComponent component,
        RemoteControlTargetActionMessage args)
    {
        if (!TryGetControlledEntity(component, args.Actor, out var remoteEntity))
            return;

        var action = GetEntity(args.Action);
        if (!GetRemoteActionEntities(remoteEntity).Contains(action)
            || !HasComp<TargetActionComponent>(action))
            return;

        var target = args.Target is { } targetNet ? GetEntity(targetNet) : (EntityUid?)null;
        _actions.TryPerformAction(remoteEntity, action, target, args.Coordinates);
    }

    private void OnRemoteInteraction(EntityUid uid, RemoteControlConsoleComponent component,
        RemoteControlInteractionMessage args)
    {
        if (!TryGetControlledEntity(component, args.Actor, out var remoteEntity))
            return;

        var target = args.Target is { } targetNet ? GetEntity(targetNet) : (EntityUid?)null;

        if (args.Action == RemoteControlInteractionAction.Drop)
        {
            _hands.TryDrop(remoteEntity, GetCoordinates(args.Coordinates));
            RefreshRemoteState(uid, component, remoteEntity);
            return;
        }

        if (args.Action == RemoteControlInteractionAction.Shoot)
        {
            if (TryComp<CombatModeComponent>(remoteEntity, out var shootCombatMode)
                && shootCombatMode.IsInCombatMode
                && EntityManager.System<SharedGunSystem>().TryGetGun(remoteEntity, out var gun)
                && gun.Comp.UseKey != args.AltInteract)
                EntityManager.System<SharedGunSystem>().AttemptShoot(remoteEntity, gun,
                    GetCoordinates(args.Coordinates), target);

            RefreshRemoteState(uid, component, remoteEntity);
            return;
        }

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
            && _hands.TryPickupAnyHand(remoteEntity, item, false, handsComp: hands, item: itemComp))
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
            && EntityManager.System<SharedMeleeWeaponSystem>()
                .TryGetWeapon(remoteEntity, out var weaponUid, out var weapon))
        {
            EntityManager.System<SharedMeleeWeaponSystem>()
                .AttemptLightAttack(remoteEntity, weaponUid, weapon, attackTarget);
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
            case RemoteControlHandAction.CycleActiveReverse:
                _hands.TryCycleActiveHandReverse(remoteEntity);
                break;
            case RemoteControlHandAction.InteractWithHand:
                _hands.TryInteractHandWithActiveHand(remoteEntity, args.Hand);
                break;
        }

        RefreshRemoteState(uid, component, remoteEntity);
    }

    private void OnRemoteInventory(EntityUid uid, RemoteControlConsoleComponent component,
        RemoteControlInventoryMessage args)
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
                {
                    var wasOpen = _ui.IsUiOpen(storageItem, StorageComponent.StorageUiKey.Key, remoteEntity);
                    _storage.OpenStorageUI(storageItem, remoteEntity, storage, false);

                    if (!wasOpen && _ui.IsUiOpen(storageItem, StorageComponent.StorageUiKey.Key, remoteEntity))
                        TrackRemoteStorageUi(uid, storageItem, args.Actor, remoteEntity);
                }

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

    private bool TryGetControlledEntity(RemoteControlConsoleComponent component, EntityUid actor,
        out EntityUid remoteEntity)
    {
        remoteEntity = default;
        return component.Controller == actor && TryGetRemoteEntity(component, out remoteEntity);
    }

    public bool TryGetControlledEntity(EntityUid actor, out EntityUid remoteEntity)
    {
        foreach (var console in EntityQuery<RemoteControlConsoleComponent>())
            if (console.Controller == actor && TryGetRemoteEntity(console, out remoteEntity))
                return true;

        remoteEntity = default;
        return false;
    }

    public bool TryGetControllerForRemoteEntity(EntityUid remoteEntity, out EntityUid controller) =>
        TryFindConsole(remoteEntity, out _, out controller);

    [SubscribeLocalEvent]
    private void OnRemoteBorgTypeAction(BorgToggleSelectTypeEvent args)
    {
        var remoteEntity = TryGetControlledEntity(args.Performer, out var controlledEntity)
            ? controlledEntity
            : args.Performer;

        if (TryOpenRemoteUi<BorgSwitchableTypeComponent>(remoteEntity, BorgSwitchableTypeUiKey.SelectBorgType))
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
        var hasConsole = TryFindConsole(args.Actor, out var console, out var controller);

        if (!hasConsole)
            return;

        if (args.Actor == controller)
            return;

        if (!_playerManager.TryGetSessionByEntity(controller, out var controllerSession))
            return;

        if (HasComp<QuickConstructableComponent>(entity.Owner))
            return;

        if (TryComp<ActivatableUIComponent>(entity.Owner, out var activatable)
            && activatable.SingleUser
            && activatable.Key?.Equals(args.UiKey) == true
            && activatable.CurrentSingleUser == args.Actor)
            _activatableUi.SetCurrentSingleUser(entity.Owner, controller, activatable);

        if (!args.UiKey.Equals(StorageComponent.StorageUiKey.Key))
        {
            _remoteUiRangeOverrides.TryAdd((entity.Owner, args.UiKey, args.Actor), 0);
            _remoteUiRangeOverrides.TryAdd((entity.Owner, args.UiKey, controller), 0);
        }

        _pendingRemoteUiMirrors.Add((entity.Owner, args.UiKey, controller));
        if (args.UiKey.Equals(StorageComponent.StorageUiKey.Key))
            TrackRemoteStorageUi(console.Owner, entity.Owner, controller, args.Actor);
    }

    private void TrackRemoteStorageUi(EntityUid console, EntityUid storage, EntityUid controller, EntityUid remote)
        => _remoteStorageUiActors[console] = (storage, controller, remote);

    [SubscribeLocalEvent]
    private void OnRemoteUiClosed(Entity<UserInterfaceComponent> entity, ref BoundUIClosedEvent args)
    {
        _remoteUiRangeOverrides.TryRemove((entity.Owner, args.UiKey, args.Actor), out _);

        if (args.UiKey.Equals(StorageComponent.StorageUiKey.Key))
            foreach (var (console, storageUi) in _remoteStorageUiActors.ToArray())
            {
                if (storageUi.Storage != entity.Owner
                    || (storageUi.Controller != args.Actor && storageUi.Remote != args.Actor))
                    continue;

                _remoteStorageUiActors.Remove(console);
                _pendingRemoteUiMirrors.Remove((entity.Owner, args.UiKey, storageUi.Controller));
                _remoteUiRangeOverrides.TryRemove((entity.Owner, args.UiKey, storageUi.Controller), out _);
                _remoteUiRangeOverrides.TryRemove((entity.Owner, args.UiKey, storageUi.Remote), out _);
            }

        if (HasComp<QuickConstructableComponent>(entity.Owner))
            return;

        if (TryFindConsole(args.Actor, out _, out var controller))
        {
            _ui.CloseUi((entity.Owner, null), args.UiKey, controller);
            return;
        }

        if (TryGetControlledEntity(args.Actor, out var remoteEntity))
            _ui.CloseUi((entity.Owner, null), args.UiKey, remoteEntity);
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
            args.Result = _interaction.InRangeUnobstructed(remoteEntity, entity.Owner, args.Data.InteractionRange)
                ? BoundUserInterfaceRangeResult.Pass
                : BoundUserInterfaceRangeResult.Fail;
    }

    private bool TryFindConsole(EntityUid remoteEntity, out Entity<RemoteControlConsoleComponent> console,
        out EntityUid controller)
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
        if (!args.Actor.Valid)
            return;

        if (!component.Users.Remove(args.Actor))
            return;

        if (component.Controller == args.Actor)
            SetController(uid, component, null,
                TryGetRemoteEntity(component, out var controlledEntity) ? controlledEntity : null);

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
            CleanupUsers(entity, remoteEntity, false);
            SetRemoteTypingState(remoteEntity, TypingIndicatorState.None);
        }

        if (TryGetBorg(args.Sink, out var chassis))
            DeactivateRemoteBorg(entity.Comp, chassis);

        entity.Comp.RemoteBrain = null;
        SetDisconnectedState(entity.Owner);
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

        if (!_borg.TryActivate((chassis, chassisComp), allowRemoteControl: true)) return chassisComp.Active;

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

        foreach (var (uiEntity, uiKey, controller) in _pendingRemoteUiMirrors)
        {
            if (!_playerManager.TryGetSessionByEntity(controller, out _))
                continue;

            _ui.OpenUi((uiEntity, null), uiKey, controller);

            if (TryComp<UserInterfaceComponent>(uiEntity, out var uiComponent))
                Dirty(uiEntity, uiComponent);
        }

        _pendingRemoteUiMirrors.Clear();

        foreach (var chassis in _pendingBorgRefreshes)
            RefreshRemoteConsoleStates(chassis);

        _pendingBorgRefreshes.Clear();

        foreach (var console in EntityQuery<RemoteControlConsoleComponent>())
        {
            if (console.Controller is not { } controller
                || _actionBlocker.CanConsciouslyPerformAction(controller)
                || !TryGetRemoteEntity(console, out var remoteEntity))
                continue;

            SetController(console.Owner, console, null, remoteEntity);
        }

        UpdatePendingRemoteBorgActivations();
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

    private void CleanupUsers(Entity<RemoteControlConsoleComponent> entity, EntityUid remoteEntity,
        bool clearUsers = true, bool clearController = true)
    {
        var controllingUser = entity.Comp.Controller;
        DeactivateRemoteBorg(entity.Comp, remoteEntity);
        if (clearController)
        {
            entity.Comp.Controller = null;
            _pendingRemoteBorgActivations.RemoveWhere(pending => pending.Console == entity.Owner);
            DeactivateToyRemote(entity.Owner, entity.Comp);
        }

        foreach (var user in entity.Comp.Users)
        {
            RemovePvsOverride(remoteEntity, user);

            if (user == controllingUser
                && TryComp<RelayInputMoverComponent>(user, out var relay)
                && relay.RelayEntity == remoteEntity)
            {
                if (entity.Comp.PreviousRelayEntity is { } previousRelay && Exists(previousRelay))
                    _mover.SetRelay(user, previousRelay);
                else
                    RemComp(user, relay);
            }
        }

        entity.Comp.PreviousRelayEntity = null;

        if (clearUsers)
            entity.Comp.Users.Clear();
    }

    private bool SetController(EntityUid consoleUid, RemoteControlConsoleComponent component, EntityUid? controller,
        EntityUid? remoteEntity, bool refreshUi = true)
    {
        if (controller is { } controlRequester
            && remoteEntity is { } controlledEntity
            && !CanRemoteControlTarget(consoleUid, component, controlledEntity, controlRequester))
        {
            if (refreshUi)
                RefreshRemoteState(consoleUid, component, controlledEntity);

            return false;
        }

        if (controller is { } takeoverRequester
            && remoteEntity is { } requestedRemoteEntity
            && !component.CanForceRemoteControl)
        {
            var conflictQuery = EntityQueryEnumerator<RemoteControlConsoleComponent>();
            while (conflictQuery.MoveNext(out var otherConsoleUid, out var otherConsole))
            {
                if (otherConsoleUid == consoleUid
                    || otherConsole.Controller is not { } otherController
                    || otherController == takeoverRequester
                    || !TryGetRemoteEntity(otherConsole, out var otherRemoteEntity)
                    || otherRemoteEntity != requestedRemoteEntity)
                    continue;

                _popup.PopupEntity(
                    Loc.GetString("remote-control-target-already-controlled"),
                    consoleUid,
                    takeoverRequester);
                if (refreshUi)
                    RefreshRemoteState(consoleUid, component, requestedRemoteEntity);

                return false;
            }
        }

        if (controller is { } requestedController)
        {
            var query = EntityQueryEnumerator<RemoteControlConsoleComponent>();
            while (query.MoveNext(out var otherConsoleUid, out var otherConsole))
            {
                if (otherConsoleUid == consoleUid
                    || otherConsole.Controller is not { } otherController)
                    continue;

                var controlsSameTarget = remoteEntity is { } requestedTarget
                                        && TryGetRemoteEntity(otherConsole, out var otherTarget)
                                        && otherTarget == requestedTarget;
                var controlsBySameUser = otherController == requestedController;
                if (!controlsSameTarget && !controlsBySameUser)
                    continue;

                if (controlsSameTarget && otherConsole.BorgActivatedByRemote)
                {
                    component.BorgActivatedByRemote = true;
                    otherConsole.BorgActivatedByRemote = false;
                }

                if (controlsSameTarget && !controlsBySameUser)
                    _popup.PopupEntity(
                        Loc.GetString("remote-control-control-taken-over"),
                        otherConsoleUid,
                        otherController);

                var otherRemoteEntity = TryGetRemoteEntity(otherConsole, out var otherRemote)
                    ? otherRemote
                    : (EntityUid?)null;
                SetController(otherConsoleUid, otherConsole, null, otherRemoteEntity);
            }
        }

        if (component.Controller is { } previousController && previousController != controller)
            CloseMirroredStorageUis(consoleUid, previousController);

        if (component.Controller is { } oldController
            && TryGetBody(component, out var oldBody)
            && TryComp<RelayInputMoverComponent>(oldController, out var oldRelay)
            && oldRelay.RelayEntity == oldBody)
        {
            if (component.PreviousRelayEntity is { } previousRelay && Exists(previousRelay))
                _mover.SetRelay(oldController, previousRelay);
            else
                RemComp(oldController, oldRelay);
        }

        component.PreviousRelayEntity = null;

        if (component.Controller is not null
            && controller is null
            && remoteEntity is { } remoteToClear
            && !TerminatingOrDeleted(remoteToClear))
            SetRemoteTypingState(remoteToClear, TypingIndicatorState.None);

        if (controller is null && component.Controller is not null && remoteEntity is { } chassis)
            DeactivateRemoteBorg(component, chassis);
        else if (controller is not null && component.Controller != controller && remoteEntity is { } chassisToActivate)
        {
            TryActivateRemoteBorg(component, chassisToActivate);
            if (TryComp<BorgChassisComponent>(chassisToActivate, out var chassisComp) && !chassisComp.Active)
                _pendingRemoteBorgActivations.Add((consoleUid, chassisToActivate));
        }

        var controllerBeforeUpdate = component.Controller;
        component.Controller = controller;

        if (controllerBeforeUpdate is { } disconnectedController
            && disconnectedController != controller
            && remoteEntity is { } remoteUiEntity
            && !TerminatingOrDeleted(remoteUiEntity))
            CloseRemoteBorgTypeSelectionUi(remoteUiEntity, disconnectedController);

        if (controller is null)
        {
            _pendingRemoteBorgActivations.RemoveWhere(pending => pending.Console == consoleUid);
            DeactivateToyRemote(consoleUid, component);
        }

        if (controller is { } newController && TryGetBody(component, out var body))
        {
            if (TryComp<RelayInputMoverComponent>(newController, out var existingRelay))
                component.PreviousRelayEntity = existingRelay.RelayEntity;

            _mover.SetRelay(newController, body);
        }

        if (refreshUi && remoteEntity is { } refreshEntity)
            RefreshRemoteState(consoleUid, component, refreshEntity);

        return true;
    }

    private void CloseMirroredStorageUis(EntityUid consoleUid, EntityUid controller)
    {
        if (!_remoteStorageUiActors.TryGetValue(consoleUid, out var storageUi)
            || storageUi.Controller != controller)
            return;

        _ui.CloseUi(storageUi.Storage, StorageComponent.StorageUiKey.Key, storageUi.Remote);
        _ui.CloseUi(storageUi.Storage, StorageComponent.StorageUiKey.Key, storageUi.Controller);
        _remoteUiRangeOverrides.TryRemove((storageUi.Storage, StorageComponent.StorageUiKey.Key, storageUi.Remote),
            out _);
        _remoteUiRangeOverrides.TryRemove((storageUi.Storage, StorageComponent.StorageUiKey.Key, storageUi.Controller),
            out _);
        _pendingRemoteUiMirrors.Remove((storageUi.Storage, StorageComponent.StorageUiKey.Key, storageUi.Controller));
        _remoteStorageUiActors.Remove(consoleUid);
    }

    private void CloseRemoteBorgTypeSelectionUi(EntityUid remoteEntity, EntityUid controller)
    {
        var uiKey = BorgSwitchableTypeUiKey.SelectBorgType;
        _pendingRemoteUiMirrors.Remove((remoteEntity, uiKey, controller));
        _ui.CloseUi((remoteEntity, null), uiKey, controller);
        _remoteUiRangeOverrides.TryRemove((remoteEntity, uiKey, controller), out _);
    }

    private bool CanRemoteControlTarget(EntityUid consoleUid, RemoteControlConsoleComponent component,
        EntityUid remoteEntity, EntityUid controller)
    {
        if (component.CanForceRemoteControl || !HasComp<ActorComponent>(remoteEntity))
            return true;

        _popup.PopupEntity(Loc.GetString("remote-control-target-player-controlled"), consoleUid, controller);
        return false;
    }

    private void DeactivateToyRemote(EntityUid consoleUid, RemoteControlConsoleComponent component)
    {
        if (component.EnableRemoteView
            || !TryComp<ItemToggleComponent>(consoleUid, out var toggle)
            || !toggle.Activated)
            return;

        _itemToggle.TryDeactivate((consoleUid, toggle), predicted: false, showPopup: false);
    }

    private void RefreshRemoteState(EntityUid consoleUid, RemoteControlConsoleComponent component,
        EntityUid remoteEntity)
    {
        foreach (var user in component.Users)
            if (_playerManager.TryGetSessionByEntity(user, out var session))
                AddRemotePvsOverrides(remoteEntity, user, session);

        var actionEntities = component.EnableRemoteView
            ? GetRemoteActionEntities(remoteEntity)
            : new HashSet<EntityUid>();
        var quickConstructionItem = component.EnableRemoteView ? GetRemoteQuickConstructionItem(remoteEntity) : null;
        _ui.SetUiState(consoleUid, RemoteControlUIKey.Key,
            new RemoteControlConsoleBuiState
            {
                Connected = true,
                EnableRemoteView = component.EnableRemoteView,
                RemoteEntity = GetNetEntity(remoteEntity),
                Controller = component.Controller is { } controllerUser ? GetNetEntity(controllerUser) : null,
                Actions = GetRemoteActions(actionEntities),
                QuickConstructionItem = quickConstructionItem is { } item ? GetNetEntity(item) : null,
                Hands =
                    component.EnableRemoteView
                        ? GetRemoteHands(remoteEntity)
                        : Array.Empty<RemoteControlHandState>(),
                Inventory =
                    component.EnableRemoteView
                        ? GetRemoteInventory(remoteEntity)
                        : Array.Empty<RemoteControlInventorySlotState>(),
                SelectedAction = component.EnableRemoteView ? GetSelectedRemoteAction(remoteEntity) : null
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
            return Array.Empty<RemoteControlHandState>();

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
                Active = hands.ActiveHandId == hand
            });
        }

        return result.ToArray();
    }

    private RemoteControlInventorySlotState[] GetRemoteInventory(EntityUid remoteEntity)
    {
        if (!TryComp<InventoryComponent>(remoteEntity, out var inventory))
            return Array.Empty<RemoteControlInventorySlotState>();

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
                HasStorage = item is { } storageItem && HasComp<StorageComponent>(storageItem)
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
            foreach (var action in _actions.GetActions(remoteEntity, actions))
                result.Add(action.Owner);

        if (TryComp<BorgSwitchableTypeComponent>(remoteEntity, out var switchable)
            && switchable.SelectTypeAction is { } selectTypeAction
            && Exists(selectTypeAction))
            result.Add(selectTypeAction);

        if (!TryComp<BorgChassisComponent>(remoteEntity, out var chassis))
            return result;

        foreach (var module in chassis.ModuleContainer.ContainedEntities)
            if (TryComp<SelectableBorgModuleComponent>(module, out var selectable)
                && selectable.ModuleSwapActionEntity is { } action
                && Exists(action))
                result.Add(action);

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
            foreach (var module in chassis.ModuleContainer.ContainedEntities)
            {
                _pvsOverride.AddSessionOverride(module, session);
                actionOverrides.Add(module);
            }

        if (TryComp<HandsComponent>(remoteEntity, out var hands))
            foreach (var hand in hands.SortedHands)
            {
                if (!_hands.TryGetHeldItem((remoteEntity, hands), hand, out var held))
                    continue;

                _pvsOverride.AddSessionOverride(held.Value, session);
                actionOverrides.Add(held.Value);
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
            foreach (var action in actionOverrides)
                _pvsOverride.RemoveSessionOverride(action, session);
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

    #region Chat
    private void OnRemoteControllerTypingChanged(TypingChangedEvent ev, EntitySessionEventArgs args)
    {
        var controller = args.SenderSession.AttachedEntity;
        if (controller is not { } controllerEntity)
            return;

        foreach (var console in EntityQuery<RemoteControlConsoleComponent>())
        {
            if (!console.EnableRemoteView
                || console.Controller != controllerEntity
                || !TryGetRemoteEntity(console, out var remoteEntity)
                || TerminatingOrDeleted(remoteEntity))
                continue;

            SetRemoteTypingState(remoteEntity, ev.State);
        }
    }

    private void SetRemoteTypingState(EntityUid remoteEntity, TypingIndicatorState state)
    {
        EnsureComp<AppearanceComponent>(remoteEntity);
        EnsureComp<TypingIndicatorComponent>(remoteEntity);
        _appearance.SetData(remoteEntity, TypingIndicatorVisuals.State, state);
    }

    [SubscribeLocalEvent]
    private void OnExpandRemoteControlChatRecipients(ExpandICChatRecipientsEvent ev)
    {
        if (TerminatingOrDeleted(ev.Source))
            return;

        var sourceXform = Transform(ev.Source);

        foreach (var console in EntityQuery<RemoteControlConsoleComponent>())
        {
            if (!console.EnableRemoteView
                || !TryGetRemoteEntity(console, out var remoteEntity)
                || TerminatingOrDeleted(remoteEntity))
                continue;

            var remoteXform = Transform(remoteEntity);
            if (console.Controller is { } controller
                && _playerManager.TryGetSessionByEntity(controller, out var controllerSession)
                && sourceXform.MapID == remoteXform.MapID
                && sourceXform.Coordinates.TryDistance(EntityManager, remoteXform.Coordinates, out var controllerDistance)
                && WithinListenerRange(remoteEntity, ev.VoiceRange, controllerDistance, ev.IsWhisper))
                ev.Recipients.TryAdd(controllerSession, new ICChatRecipientData(controllerDistance, false, RemoteListener: remoteEntity));

            if (console.Controller != ev.Source)
                continue;

            foreach (var session in _playerManager.Sessions)
            {
                if (session.AttachedEntity is not { Valid: true } listener
                    || listener == remoteEntity
                    || ev.Recipients.ContainsKey(session))
                    continue;

                var listenerXform = Transform(listener);
                if (listenerXform.MapID != remoteXform.MapID
                    || !remoteXform.Coordinates.TryDistance(EntityManager, listenerXform.Coordinates, out var distance)
                    || !WithinListenerRange(listener, ev.VoiceRange, distance, ev.IsWhisper))
                    continue;

                ev.Recipients.Add(session, new ICChatRecipientData(distance, false, RemoteSource: remoteEntity));
            }
        }
    }

    private bool WithinListenerRange(EntityUid listener, float baseRange, float distance, bool isWhisper)
    {
        if (!TryComp<ChatListenerRangeComponent>(listener, out var range)
            || !range.AllowExtendListenRange)
            return distance < baseRange;

        var extendedRange = isWhisper ? range.WhisperMuffledRange : range.VoiceRange;
        return distance < Math.Max(baseRange, extendedRange);
    }
    #endregion
}
