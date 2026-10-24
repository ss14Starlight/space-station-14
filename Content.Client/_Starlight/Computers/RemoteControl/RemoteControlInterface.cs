using Content.Shared.Hands.EntitySystems;
using Content.Shared._Starlight.Computers.RemoteControl;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Storage.Controls;
using System.Numerics;

namespace Content.Client._Starlight.Computers.RemoteControl;

public sealed partial class RemoteControlInterface : EntitySystem
{
    private RemoteControlConsoleWindow? _window;
    private TimeSpan _nextRemoteAlertUpdate;
    private EntityUid? _consoleEntity;
    private IEye? _consoleEye;
    private IViewportControl? _consoleViewport;
    private EntityUid? _bodyControlEntity;

    public EntityUid? ControlledEntity { get; private set; }
    public EntityUid? ConsoleControlledEntity => _bodyControlEntity == null ? _consoleEntity : null;
    public IEye? ControlledEye { get; private set; }
    public IViewportControl? ControlledViewport { get; private set; }
    public IViewportControl? RemoteViewport { get; private set; }
    public IEye? RemoteViewportEye => _window is { Disposed: false } window ? window.RemoteEye : null;
    public MapCoordinates? RemoteMousePosition { get; private set; }
    public event Action<EntityUid?>? ControlledEntityChanged;
    public event Action<EntityUid, bool, RemoteControlInteractionAction>? InteractionRequested;
    public event Action<string>? ItemConstructionRequested;
    public event Action<EntityUid, bool, RemoteControlInteractionAction>? BodyInteractionRequested;

    public override void Initialize() => base.Initialize();

    public override void Shutdown() => base.Shutdown();

    public void SetStatusWindow(RemoteControlConsoleWindow? window)
    {
        _window = window;
        RemoteViewport = window?.RemoteViewport;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_window is not { Disposed: false } window)
            return;

        window.UpdateRemoteEye();

        var curTime = IoCManager.Resolve<IGameTiming>().CurTime;
        if (curTime < _nextRemoteAlertUpdate)
            return;

        _nextRemoteAlertUpdate = curTime + TimeSpan.FromSeconds(1);
        window.UpdateRemoteStatus();
    }

    public void SetControlledEntity(EntityUid? entity, IEye? eye = null, IViewportControl? viewport = null)
    {
        _consoleEntity = entity;
        _consoleEye = eye;
        _consoleViewport = viewport;
        if (_bodyControlEntity == null)
            UpdateControlledEntity(entity, eye, viewport);
    }

    /// <summary>The native world view may use the same examine, verb and UI hooks as a console viewport.</summary>
    public void SetBodyControlEntity(EntityUid? entity, IEye? eye = null)
    {
        if (_bodyControlEntity != entity)
            RemoteMousePosition = null;
        _bodyControlEntity = entity;
        UpdateControlledEntity(entity ?? _consoleEntity, entity != null ? eye : _consoleEye,
            entity != null ? null : _consoleViewport);
    }

    private void UpdateControlledEntity(EntityUid? entity, IEye? eye, IViewportControl? viewport)
    {
        if (ControlledEntity == entity)
        {
            ControlledEye = eye;
            ControlledViewport = viewport;
            return;
        }

        ControlledEntity = entity;
        ControlledEye = eye;
        ControlledViewport = viewport;
        if (entity is null)
            RemoteMousePosition = null;

        ControlledEntityChanged?.Invoke(entity);
    }

    public bool TryEmbedWindow(EntityUid owner, BaseWindow window)
    {
        if (ControlledEntity != owner
            || !TryEmbedWindow(window))
            return false;

        return true;
    }

    public bool TryEmbedWindow(BaseWindow window)
    {
        if (ControlledEntity is null
            || _bodyControlEntity != null
            || _window is not { Disposed: false } remoteWindow
            || window.Disposed)
            return false;

        if (window is StorageWindow storageWindow)
        {
            if (window.Parent != remoteWindow.RemoteStorageContainer)
            {
                window.Orphan();
                remoteWindow.RemoteStorageContainer.AddChild(window);
            }

            storageWindow.VerticalAlignment = Control.VAlignment.Center;
            return true;
        }

        if (window.Parent != remoteWindow.RootContainer)
        {
            window.Orphan();
            remoteWindow.RootContainer.AddChild(window);
        }

        window.RecenterWindow(new Vector2(0.5f, 0.5f));
        return true;
    }

    public void SetRemoteMousePosition(MapCoordinates? position)
    {
        if (_bodyControlEntity == null)
            RemoteMousePosition = position;
    }

    public bool TryRequestItemConstruction(string prototypeName)
    {
        if (ControlledEntity is null || _bodyControlEntity != null)
            return false;

        ItemConstructionRequested?.Invoke(prototypeName);
        return true;
    }

    public bool OpenRemoteRadialMenu(SimpleRadialMenu menu)
        => _window?.OpenRemoteRadialMenu(menu) ?? false;

    public bool IsControlledEntityOrActiveItem(EntityUid entity)
        => ControlledEntity is { } remoteEntity
            && (remoteEntity == entity
                || (EntityManager.System<SharedHandsSystem>().TryGetActiveItem(remoteEntity, out var heldItem)
                    && heldItem == entity));

    public EntityUid? GetRadialMenuTrackingEntity(EntityUid owner, EntityUid? localEntity)
        => ControlledEntity is null
            ? owner
            : IsControlledEntityOrActiveItem(owner)
                ? localEntity ?? owner
                : null;

    public void RequestInteraction(EntityUid target, bool altInteract, RemoteControlInteractionAction action = RemoteControlInteractionAction.Interact)
    {
        if (_bodyControlEntity != null)
            BodyInteractionRequested?.Invoke(target, altInteract, action);
        else
            InteractionRequested?.Invoke(target, altInteract, action);
    }
}
