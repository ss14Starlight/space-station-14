using Content.Shared.Hands.EntitySystems;
using Content.Shared._Starlight.Computers.RemoteControl;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Client.UserInterface.CustomControls;
using Content.Client.UserInterface.Controls;
using System.Numerics;

namespace Content.Client._Starlight.Computers.RemoteControl;

public sealed partial class RemoteControlInterface : EntitySystem
{
    private RemoteControlConsoleWindow? _window;
    private TimeSpan _nextRemoteAlertUpdate;

    public EntityUid? ControlledEntity { get; private set; }
    public IEye? ControlledEye { get; private set; }
    public IViewportControl? ControlledViewport { get; private set; }
    public MapCoordinates? RemoteMousePosition { get; private set; }
    public event Action<EntityUid?>? ControlledEntityChanged;
    public event Action<EntityUid, bool, RemoteControlInteractionAction>? InteractionRequested;
    public event Action<string>? ItemConstructionRequested;

    public void SetStatusWindow(RemoteControlConsoleWindow? window)
        => _window = window;

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
            || _window is not { Disposed: false } remoteWindow
            || window.Disposed)
            return false;

        if (window.Parent != remoteWindow.RootContainer)
        {
            window.Orphan();
            remoteWindow.RootContainer.AddChild(window);
        }

        window.RecenterWindow(new Vector2(0.5f, 0.5f));
        return true;
    }

    public void SetRemoteMousePosition(MapCoordinates? position)
        => RemoteMousePosition = position;

    public bool TryRequestItemConstruction(string prototypeName)
    {
        if (ControlledEntity is null)
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
        => InteractionRequested?.Invoke(target, altInteract, action);
}
