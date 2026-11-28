using Content.Client.Eye;
using Content.Client.Construction;
using Content.Client._DEN.QuickConstruction.UI;
using Content.Client._Starlight.UserInterface;
using Content.Shared.Alert;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared._DEN.QuickConstruction.Events;
using Content.Shared.Atmos.Components;
using Content.Shared.Actions.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.Laws.Components;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Client.Player;
using System.Collections.Generic;
using System.Linq;

namespace Content.Client._Starlight.Computers.RemoteControl;

[UsedImplicitly]
public sealed class RemoteControlConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private readonly IPlayerManager _playerManager = IoCManager.Resolve<IPlayerManager>();
    private readonly IGameTiming _gameTiming = IoCManager.Resolve<IGameTiming>();
    private EyeLerpingSystem? _eyeLerpingSystem;
    private RemoteControlInterface _remoteControl = default!;
    private RemoteControlConsoleWindow? _window;
    private EntityUid? _remoteEntity;
    private QuickConstructionBoundUserInterface? _remoteQuickConstruction;
    private EntityUid? _remoteQuickConstructionItem;
    private readonly List<NetEntity> _remoteActionEntities = new();
    private readonly List<EntityUid> _shownActionEntities = new();
    private EntityUid? _selectedAction;
    private EntityUid? _shownSelectedAction;
    protected override void Open()
    {
        if (IsOpened && _window is { Disposed: false })
            return;

        base.Open();
        _eyeLerpingSystem = EntMan.System<EyeLerpingSystem>();
        _remoteControl = EntMan.System<RemoteControlInterface>();
        _remoteControl.InteractionRequested += OnRemoteMenuInteraction;
        _remoteControl.ItemConstructionRequested += OnRemoteItemConstruction;
        _window = this.CreatePopOutableWindow<RemoteControlConsoleWindow>(EntMan);
        _window.ToggleControl += ToggleControl;
        _window.SettingsRequested += OpenSettings;
        _window.RemoteToggleKnockdownPressed += RemoteToggleKnockdown;
        _window.RemoteReleasePulledObjectPressed += RemoteReleasePulledObject;
        _window.RemoteInteractionPressed += RemoteInteractionPressed;
        _window.RemoteDragDropPressed += RemoteDragDropPressed;
        _window.RemoteActionPressed += RemoteActionPressed;
        _window.RemoteTargetActionPressed += RemoteTargetActionPressed;
        _window.RemoteAlertPressed += RemoteAlertPressed;
        _window.RemoteHandPressed += RemoteHandPressed;
        _window.RemoteInventoryPressed += RemoteInventoryPressed;
        _window.InitializeViewport();
        _remoteControl.SetStatusWindow(_window);
        _window.OnFinalClose += OnWindowClosed;
    }

    private void OnWindowClosed()
    {
        _window?.CancelRemoteAttackHolds();
        Close();
    }

    private void OpenSettings()
        => SendMessage(new RemoteControlOpenSettingsMessage());

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not RemoteControlConsoleBuiState remoteState
            || _window is not { Disposed: false } window)
            return;

        if (remoteState.RemoteEntity is { } entity && EntMan.TryGetEntity(entity, out EntityUid? remoteEntity)
            && remoteEntity is { } remoteUid
            && EntMan.TryGetComponent<EyeComponent>(remoteUid, out var eye))
        {
            if (_remoteEntity != remoteUid)
            {
                if (_remoteEntity is { } oldEntity)
                    _eyeLerpingSystem?.RemoveEye(oldEntity);

                _eyeLerpingSystem?.AddEye(remoteUid);
                _remoteEntity = remoteUid;
            }

            window.SetRemoteEye((IEye) eye.Eye);
            window.SetRemoteEntity(remoteUid);
        }
        else
        {
            if (_remoteEntity is { } oldEntity)
            {
                _eyeLerpingSystem?.RemoveEye(oldEntity);
                _remoteEntity = null;
            }

            window.SetRemoteEye(null);
            window.SetRemoteEntity(null);
        }

        window.SetRemoteAlerts(remoteState.Alerts);
        window.SetRemoteViewEnabled(remoteState.EnableRemoteView);
        window.SetConnected(remoteState.Connected);
        var controlling = remoteState.Controller is { } controller
            && EntMan.TryGetEntity(controller, out var controllerEntity)
            && controllerEntity == _playerManager.LocalEntity;

        window.SetControlState(controlling, remoteState.Controller != null);
        _remoteControl.SetControlledEntity(controlling ? _remoteEntity : null,
            controlling ? window.RemoteEye : null,
            controlling ? window.RemoteViewport : null);

        _remoteActionEntities.Clear();
        _remoteActionEntities.AddRange(remoteState.Actions);
        _selectedAction = remoteState.SelectedAction is { } selected
            && EntMan.TryGetEntity(selected, out var selectedEntity)
            ? selectedEntity
            : null;
        RefreshActions();
        _window.SetHands(remoteState.Hands);
        _window.SetInventory(remoteState.Inventory);

        if (remoteState.QuickConstructionItem is { } quickItem
            && EntMan.TryGetEntity(quickItem, out EntityUid? quickItemEntity)
            && quickItemEntity is { } quickItemUid)
        {
            if (_remoteQuickConstructionItem != quickItemUid)
            {
                ClearRemoteQuickConstruction();
                _remoteQuickConstruction = new QuickConstructionBoundUserInterface(quickItemUid, QuickConstructionUiKey.Key);
                _remoteQuickConstructionItem = quickItemUid;
                _remoteQuickConstruction.RemoteMenuClosed += OnRemoteQuickConstructionMenuClosed;
                _remoteQuickConstruction.OpenRemote();
            }
        }
        else
            ClearRemoteQuickConstruction();
    }

    public override void Update()
    {
        base.Update();
        RefreshActions();

        if (_remoteEntity is { } remoteEntity && _window is { Disposed: false } window)
            window.RefreshPredictedRemoteHands(remoteEntity);
    }

    private void RefreshActions()
    {
        if (_window == null)
            return;

        _selectedAction = _remoteEntity switch
        {
            { } remoteEntity when EntMan.TryGetComponent<BorgChassisComponent>(remoteEntity, out var chassis)
                => chassis.SelectedModule switch
                {
                    { } module when EntMan.TryGetComponent<SelectableBorgModuleComponent>(module, out var selectable)
                        => selectable.ModuleSwapActionEntity,
                    null => null,
                    _ => _selectedAction
                },
            _ => _selectedAction
        };

        var actions = new List<EntityUid>(_remoteActionEntities.Count);
        foreach (var action in _remoteActionEntities)
        {
            if (EntMan.TryGetEntity(action, out var actionEntity)
                && actionEntity is { } uid
                && EntMan.HasComponent<ActionComponent>(uid)
                && (!EntMan.TryGetComponent<InstantActionComponent>(uid, out var instantAction)
                    || instantAction.Event is not ToggleLawsScreenEvent))
                actions.Add(uid);
        }

        if (actions.Count == _shownActionEntities.Count
            && actions.SequenceEqual(_shownActionEntities)
            && _selectedAction == _shownSelectedAction)
            return;

        _shownActionEntities.Clear();
        _shownActionEntities.AddRange(actions);
        _shownSelectedAction = _selectedAction;
        _window.SetActions(actions, _selectedAction);
    }

    private void ToggleControl() => SendMessage(new RemoteControlToggleMessage());

    private void RemoteToggleKnockdown()
        => SendPredictedMessage(new RemoteControlToggleKnockdownMessage());

    private void RemoteReleasePulledObject()
        => SendPredictedMessage(new RemoteControlReleasePulledObjectMessage());

    private void RemoteActionPressed(EntityUid action)
    {
        var message = new RemoteControlActionMessage { Action = EntMan.GetNetEntity(action) };
        if (_remoteEntity is { } remoteEntity
            && EntMan.HasComponent<BorgSwitchableTypeComponent>(remoteEntity)
            && EntMan.TryGetComponent<InstantActionComponent>(action, out var instantAction)
            && instantAction.Event is BorgToggleSelectTypeEvent)
            SendMessage(message);
        else
            SendPredictedMessage(message);
    }

    private void RemoteTargetActionPressed(EntityUid action, EntityUid? target, NetCoordinates coordinates)
        => SendPredictedMessage(new RemoteControlTargetActionMessage
        {
            Action = EntMan.GetNetEntity(action),
            Target = target is { } targetEntity ? EntMan.GetNetEntity(targetEntity) : null,
            Coordinates = coordinates,
        });

    private void RemoteAlertPressed(ProtoId<AlertPrototype> alert)
    {
        if (_remoteControl.ControlledEntity is null)
            return;

        SendPredictedMessage(new RemoteControlAlertMessage { Alert = alert });
    }

    private void OnRemoteItemConstruction(string prototypeName)
        => SendMessage(new RemoteControlBuildItemConstructionMessage { PrototypeName = prototypeName });

    private void OnRemoteQuickConstructionMenuClosed()
    {
        if (_remoteQuickConstructionItem is not { } item)
            return;

        SendMessage(new RemoteControlCloseQuickConstructionMessage { Item = EntMan.GetNetEntity(item), });
    }

    private void RemoteInteractionPressed(NetCoordinates coordinates, EntityUid? target, bool altInteract,
        bool activateInWorld,
        RemoteControlInteractionAction action, AtmosPipeLayer? pipeLayer)
    {
        var remotePlacement = EntMan.System<RemoteConstructionPlacementSystem>();
        var remotePlacementActive = remotePlacement.IsActive;
        if (!altInteract
            && action == RemoteControlInteractionAction.Interact
            && remotePlacementActive
            && !remotePlacement.TryCommit(target))
            return;

        if (!altInteract
            && action == RemoteControlInteractionAction.Interact
            && target is { } ghost
            && EntMan.TryGetComponent<ConstructionGhostComponent>(ghost, out var ghostComp)
            && ghostComp.Prototype is { } prototype)
        {
            var transform = EntMan.GetComponent<TransformComponent>(ghost);
            SendMessage(new RemoteControlBuildConstructionMessage
            {
                Location = EntMan.GetNetCoordinates(transform.Coordinates),
                PrototypeName = prototype.ID,
                Angle = transform.LocalRotation,
                Ack = ghost.GetHashCode(),
            });
            return;
        }

        var predictInteraction = action switch
        {
            RemoteControlInteractionAction.Interact =>
                !remotePlacementActive && pipeLayer is null && (!activateInWorld || target is not null),
            RemoteControlInteractionAction.TryPull => target is not null,
            RemoteControlInteractionAction.Drop or RemoteControlInteractionAction.Throw
                or RemoteControlInteractionAction.Shoot or RemoteControlInteractionAction.HeavyAttack => true,
            _ => false
        };

        var message = new RemoteControlInteractionMessage
        {
            Coordinates = coordinates,
            Target = target is { } entity ? EntMan.GetNetEntity(entity) : null,
            AltInteract = altInteract,
            ActivateInWorld = activateInWorld,
            Action = action,
            Tick = _gameTiming.CurTick,
            PipeLayer = pipeLayer,
        };

        if (predictInteraction)
            SendPredictedMessage(message);
        else
            SendMessage(message);
    }

    private void OnRemoteMenuInteraction(
        EntityUid target,
        bool altInteract,
        bool activateInWorld,
        RemoteControlInteractionAction action)
    {
        if (_remoteEntity is null)
            return;

        var message = new RemoteControlInteractionMessage
        {
            Coordinates = EntMan.GetNetCoordinates(EntMan.GetComponent<TransformComponent>(target).Coordinates),
            Target = EntMan.GetNetEntity(target),
            AltInteract = altInteract,
            ActivateInWorld = activateInWorld,
            Action = action,
            Tick = _gameTiming.CurTick,
        };

        var predictInteraction = action switch
        {
            RemoteControlInteractionAction.Interact => true,
            RemoteControlInteractionAction.TryPull => true,
            RemoteControlInteractionAction.Drop or RemoteControlInteractionAction.Throw
                or RemoteControlInteractionAction.Shoot or RemoteControlInteractionAction.HeavyAttack => true,
            _ => false
        };

        if (predictInteraction)
            SendPredictedMessage(message);
        else
            SendMessage(message);
    }

    private void RemoteHandPressed(string hand, RemoteControlHandAction action)
        => SendPredictedMessage(new RemoteControlHandMessage { Hand = hand, Action = action });

    private void RemoteDragDropPressed(EntityUid dragged, EntityUid target)
        => SendMessage(new RemoteControlDragDropMessage
        {
            Dragged = EntMan.GetNetEntity(dragged),
            Target = EntMan.GetNetEntity(target),
        });

    private void RemoteInventoryPressed(string slot, RemoteControlInventoryAction action)
    {
        var message = new RemoteControlInventoryMessage { Slot = slot, Action = action };
        if (action is RemoteControlInventoryAction.UseSlot
            or RemoteControlInventoryAction.ActivateSlot
            or RemoteControlInventoryAction.AltActivateSlot)
            SendPredictedMessage(message);
        else
            SendMessage(message);
    }

    private void ClearRemoteQuickConstruction()
    {
        if (_remoteQuickConstruction is { } quickConstruction)
        {
            quickConstruction.RemoteMenuClosed -= OnRemoteQuickConstructionMenuClosed;
            quickConstruction.Dispose();
        }

        _remoteQuickConstruction = null;
        _remoteQuickConstructionItem = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _window?.CancelRemoteAttackHolds();

        if (disposing)
        {
            var window = _window;
            _window = null;
            _remoteControl.InteractionRequested -= OnRemoteMenuInteraction;
            _remoteControl.ItemConstructionRequested -= OnRemoteItemConstruction;
            _remoteControl.SetStatusWindow(null);
            _remoteControl.SetControlledEntity(null);

            if (_remoteEntity is { } remoteEntity)
                _eyeLerpingSystem?.RemoveEye(remoteEntity);
            _remoteEntity = null;

            ClearRemoteQuickConstruction();

            if (window is not null)
            {
                window.OnFinalClose -= OnWindowClosed;
                window.ToggleControl -= ToggleControl;
                window.SettingsRequested -= OpenSettings;
                window.RemoteToggleKnockdownPressed -= RemoteToggleKnockdown;
                window.RemoteReleasePulledObjectPressed -= RemoteReleasePulledObject;
                window.RemoteInteractionPressed -= RemoteInteractionPressed;
                window.RemoteDragDropPressed -= RemoteDragDropPressed;
                window.RemoteActionPressed -= RemoteActionPressed;
                window.RemoteAlertPressed -= RemoteAlertPressed;
                window.RemoteHandPressed -= RemoteHandPressed;
                window.RemoteInventoryPressed -= RemoteInventoryPressed;
                window.SetRemoteEye(null);
                window.SetRemoteEntity(null);
                window.SetConnected(false);
                window.SetControlState(false, false);
                window.DisposePopOut();
            }
        }

        base.Dispose(disposing);
    }
}
