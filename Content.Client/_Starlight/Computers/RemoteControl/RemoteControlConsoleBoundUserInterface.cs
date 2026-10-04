using Content.Client.Eye;
using Content.Client.Construction;
using Content.Client._DEN.QuickConstruction.UI;
using Content.Client._Starlight.UserInterface;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared._DEN.QuickConstruction.Events;
using Content.Shared.Atmos.Components;
using Content.Shared.Actions.Components;
using Content.Shared.Silicons.Laws.Components;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Client.Player;
using System.Collections.Generic;
using System.Linq;

namespace Content.Client._Starlight.Computers.RemoteControl;

[UsedImplicitly]
public sealed class RemoteControlConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private readonly IPlayerManager _playerManager = IoCManager.Resolve<IPlayerManager>();
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
        _window.RemoteInteractionPressed += RemoteInteractionPressed;
        _window.RemoteActionPressed += RemoteActionPressed;
        _window.RemoteTargetActionPressed += RemoteTargetActionPressed;
        _window.RemoteHandPressed += RemoteHandPressed;
        _window.RemoteInventoryPressed += RemoteInventoryPressed;
        _window.InitializeViewport();
        _remoteControl.SetStatusWindow(_window);
        _window.OnFinalClose += OnWindowClosed;
    }

    private void OnWindowClosed() => Close();

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
                _remoteQuickConstruction?.Dispose();
                _remoteQuickConstruction = new QuickConstructionBoundUserInterface(quickItemUid, QuickConstructionUiKey.Key);
                _remoteQuickConstructionItem = quickItemUid;
                _remoteQuickConstruction.OpenRemote();
            }
        }
        else if (_remoteQuickConstruction is not null)
        {
            _remoteQuickConstruction.Dispose();
            _remoteQuickConstruction = null;
            _remoteQuickConstructionItem = null;
        }
    }

    public override void Update()
    {
        base.Update();
        RefreshActions();
    }

    private void RefreshActions()
    {
        if (_window == null)
            return;

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

    private void RemoteActionPressed(EntityUid action)
        => SendMessage(new RemoteControlActionMessage { Action = EntMan.GetNetEntity(action) });

    private void RemoteTargetActionPressed(EntityUid action, EntityUid? target, NetCoordinates coordinates)
        => SendMessage(new RemoteControlTargetActionMessage
        {
            Action = EntMan.GetNetEntity(action),
            Target = target is { } targetEntity ? EntMan.GetNetEntity(targetEntity) : null,
            Coordinates = coordinates,
        });

    private void OnRemoteItemConstruction(string prototypeName)
        => SendMessage(new RemoteControlBuildItemConstructionMessage { PrototypeName = prototypeName });

    private void RemoteInteractionPressed(NetCoordinates coordinates, EntityUid? target, bool altInteract,
        bool activateInWorld,
        RemoteControlInteractionAction action, AtmosPipeLayer? pipeLayer)
    {
        var remotePlacement = EntMan.System<RemoteConstructionPlacementSystem>();
        if (!altInteract
            && action == RemoteControlInteractionAction.Interact
            && remotePlacement.IsActive
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

        SendMessage(new RemoteControlInteractionMessage
        {
            Coordinates = coordinates,
            Target = target is { } entity ? EntMan.GetNetEntity(entity) : null,
            AltInteract = altInteract,
            ActivateInWorld = activateInWorld,
            Action = action,
            PipeLayer = pipeLayer,
        });
    }

    private void OnRemoteMenuInteraction(EntityUid target, bool altInteract, RemoteControlInteractionAction action)
    {
        if (_remoteEntity is null)
            return;

        SendMessage(new RemoteControlInteractionMessage
        {
            Coordinates = EntMan.GetNetCoordinates(EntMan.GetComponent<TransformComponent>(target).Coordinates),
            Target = EntMan.GetNetEntity(target),
            AltInteract = altInteract,
            Action = action,
        });
    }

    private void RemoteHandPressed(string hand, RemoteControlHandAction action)
        => SendMessage(new RemoteControlHandMessage { Hand = hand, Action = action });

    private void RemoteInventoryPressed(string slot, RemoteControlInventoryAction action)
        => SendMessage(new RemoteControlInventoryMessage { Slot = slot, Action = action });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

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

            _remoteQuickConstruction?.Dispose();
            _remoteQuickConstruction = null;
            _remoteQuickConstructionItem = null;

            if (window is not null)
            {
                window.OnFinalClose -= OnWindowClosed;
                window.ToggleControl -= ToggleControl;
                window.RemoteInteractionPressed -= RemoteInteractionPressed;
                window.RemoteActionPressed -= RemoteActionPressed;
                window.RemoteHandPressed -= RemoteHandPressed;
                window.RemoteInventoryPressed -= RemoteInventoryPressed;
                window.SetRemoteEye(null);
                window.SetRemoteEntity(null);
                window.SetConnected(false);
                window.SetControlState(false, false);
                window.DisposePopOut();
                window.Dispose();
            }
        }
    }
}
