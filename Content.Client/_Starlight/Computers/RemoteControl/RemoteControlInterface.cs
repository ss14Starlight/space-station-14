using Content.Shared.Hands.EntitySystems;
using Content.Shared._Starlight.Computers.RemoteControl;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Content.Client.UserInterface.Controls;

namespace Content.Client._Starlight.Computers.RemoteControl;

public sealed partial class RemoteControlInterface : EntitySystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    private IUserInterfaceManager? _uiManager;
    private RemoteControlConsoleWindow? _window;
    private TimeSpan _nextRemoteAlertUpdate;
    private readonly List<PendingBuiWindowOpen> _pendingBuiWindowOpens = [];
    private readonly List<PendingRemoteBuiOpen> _pendingRemoteBuiOpens = [];
    private EntityUid? _remoteUiOpenContext;
    private static readonly TimeSpan RemoteBuiOpenTimeout = TimeSpan.FromSeconds(5);

    public EntityUid? ControlledEntity { get; private set; }
    public IEye? ControlledEye { get; private set; }
    public IViewportControl? ControlledViewport { get; private set; }
    public IViewportControl? RemoteViewport { get; private set; }
    public UIRoot? RemoteUIRoot => _window is { Disposed: false } window ? window.RemoteUIRoot : null;
    public IEye? RemoteViewportEye => _window is { Disposed: false } window ? window.RemoteEye : null;
    public MapCoordinates? RemoteMousePosition { get; private set; }
    public event Action<EntityUid?>? ControlledEntityChanged;
    public event Action<EntityUid, bool, bool, RemoteControlInteractionAction>? InteractionRequested;
    public event Action<string>? ItemConstructionRequested;

    public override void Initialize()
    {
        base.Initialize();

        _uiManager = IoCManager.Resolve<IUserInterfaceManager>();
        _uiManager.WindowRoot.OnChildAdded += OnWindowRootChildAdded;
    }

    [SubscribeNetworkEvent]
    private void OnRemoteBuiOpenedByRemoteActor(RemoteControlBuiOpenedByRemoteActorEvent args)
    {
        if (ControlledEntity is not { } remoteEntity || GetNetEntity(remoteEntity) != args.RemoteActor)
            return;

        var curTime = IoCManager.Resolve<IGameTiming>().CurTime;
        PrunePendingBuiOpens(curTime);

        for (var i = _pendingBuiWindowOpens.Count - 1; i >= 0; i--)
        {
            var pendingOpen = _pendingBuiWindowOpens[i];
            if (pendingOpen.UiEntity != args.UiEntity
                || pendingOpen.UiKeyIdentifier != args.UiKey
                || pendingOpen.RemoteActor != args.RemoteActor)
                continue;

            if (!pendingOpen.RemoteOriginConfirmed)
            {
                pendingOpen.RemoteOriginConfirmed = true;
                TryEmbedPendingWindow(pendingOpen);
            }

            return;
        }

        _pendingRemoteBuiOpens.RemoveAll(pending => pending.UiEntity == args.UiEntity
                                                    && pending.UiKey == args.UiKey
                                                    && pending.RemoteActor == args.RemoteActor);
        _pendingRemoteBuiOpens.Add(new PendingRemoteBuiOpen(
            args.UiEntity,
            args.UiKey,
            args.RemoteActor,
            curTime + RemoteBuiOpenTimeout));
    }

    public override void Shutdown()
    {
        if (_uiManager is { } uiManager)
            uiManager.WindowRoot.OnChildAdded -= OnWindowRootChildAdded;

        base.Shutdown();
    }

    private void OnWindowRootChildAdded(Control control)
    {
        if (control is not BaseWindow window
            || window is RemoteControlConsoleWindow
            || window is RemoteControlSettingsWindow)
            return;

        PrunePendingBuiOpens(IoCManager.Resolve<IGameTiming>().CurTime);

        var pendingOpen = _pendingBuiWindowOpens.FindLast(pending => pending.Window is null);
        if (pendingOpen is null)
            return;

        pendingOpen.Window = window;
        TryEmbedPendingWindow(pendingOpen);
    }

    /// <summary>
    /// The server marks remote-origin opens because the remote actor is not included in the client's replicated UI actor state.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnBoundUiOpened(Entity<UserInterfaceComponent> entity, ref BoundUIOpenedEvent args)
    {
        if (ControlledEntity is not { } remoteEntity
            || _uiManager is not { } uiManager)
            return;

        var remoteUiContext = _remoteUiOpenContext == remoteEntity;
        if (args.Actor != _playerManager.LocalEntity
            && args.Actor != remoteEntity
            && !remoteUiContext)
            return;

        var curTime = IoCManager.Resolve<IGameTiming>().CurTime;
        PrunePendingBuiOpens(curTime);
        var uiKeyIdentifier = RemoteControlBuiOpenedByRemoteActorEvent.GetUiKeyIdentifier(args.UiKey);
        var ownerNetEntity = GetNetEntity(entity.Owner);
        var remoteActor = GetNetEntity(remoteEntity);
        var remoteOriginConfirmed = TryConsumeRemoteBuiOpen(ownerNetEntity, uiKeyIdentifier, remoteActor)
                                    || remoteUiContext;
        var pendingOpen = new PendingBuiWindowOpen(
            entity.Owner,
            args.UiKey,
            remoteEntity,
            ownerNetEntity,
            uiKeyIdentifier,
            remoteActor,
            curTime + RemoteBuiOpenTimeout,
            remoteOriginConfirmed);
        _pendingBuiWindowOpens.Add(pendingOpen);
        uiManager.DeferAction(() =>
        {
            if (pendingOpen.Window is null)
                _pendingBuiWindowOpens.Remove(pendingOpen);
        });
    }

    public void SetStatusWindow(RemoteControlConsoleWindow? window)
    {
        _window = window;
        RemoteViewport = window?.RemoteViewport;
    }

    public bool TryGetViewportEntity(in OverlayDrawArgs args, out EntityUid entity)
    {
        if (ControlledEntity is { } remoteEntity
            && ((ControlledViewport is { } remoteViewport && args.ViewportControl == remoteViewport)
                || (ControlledEye is { } remoteEye && args.Viewport.Eye == remoteEye)))
        {
            entity = remoteEntity;
            return true;
        }

        if (_playerManager.LocalSession?.AttachedEntity is { } localEntity
            && TryComp(localEntity, out EyeComponent? eye)
            && args.Viewport.Eye == eye.Eye)
        {
            entity = localEntity;
            return true;
        }

        entity = default;
        return false;
    }

    public PopupContainer? GetExaminePopupContainer(EntityUid examiner)
        => ControlledEntity == examiner && _window is { Disposed: false } window
            ? window.RemoteExaminePopupContainer
            : null;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = IoCManager.Resolve<IGameTiming>().CurTime;
        PrunePendingBuiOpens(curTime);

        if (_window is not { Disposed: false } window)
            return;

        if (curTime < _nextRemoteAlertUpdate)
            return;

        _nextRemoteAlertUpdate = curTime + TimeSpan.FromSeconds(1);
        window.UpdateRemoteStatus();
    }

    public void UpdateRemoteMeleeAttackHolds()
    {
        if (_window is { Disposed: false } window)
            window.UpdateRemoteMeleeAttackHolds();
    }

    public void SetControlledEntity(EntityUid? entity, IEye? eye = null, IViewportControl? viewport = null)
    {
        if (ControlledEntity == entity)
        {
            ControlledEye = eye;
            ControlledViewport = viewport;
            return;
        }

        _pendingBuiWindowOpens.Clear();
        _pendingRemoteBuiOpens.Clear();
        ControlledEntity = entity;
        ControlledEye = eye;
        ControlledViewport = viewport;
        if (entity is null)
            RemoteMousePosition = null;

        ControlledEntityChanged?.Invoke(entity);
    }

    public bool TryEmbedWindow(EntityUid owner, BaseWindow window)
        => ControlledEntity == owner && TryEmbedWindowInternal(window);

    public void RunWithRemoteUiOpenContext(EntityUid remoteEntity, Action action)
    {
        if (ControlledEntity != remoteEntity)
        {
            action();
            return;
        }

        var previousContext = _remoteUiOpenContext;
        _remoteUiOpenContext = remoteEntity;
        try
        {
            action();
        }
        finally
        {
            _remoteUiOpenContext = previousContext;
        }
    }

    public bool TryEmbedWindow(BoundUserInterface bui, BaseWindow window)
    {
        if (ControlledEntity is not { } remoteEntity)
            return false;

        PrunePendingBuiOpens(IoCManager.Resolve<IGameTiming>().CurTime);
        var pendingOpen = _pendingBuiWindowOpens.FindLast(pending =>
            pending.Owner == bui.Owner
            && pending.UiKey.Equals(bui.UiKey)
            && pending.RemoteEntity == remoteEntity
            && (pending.Window is null || pending.Window == window));
        if (pendingOpen is null)
            return false;

        pendingOpen.Window = window;
        return TryEmbedPendingWindow(pendingOpen);
    }

    private bool TryConsumeRemoteBuiOpen(NetEntity owner, string uiKey, NetEntity remoteActor)
    {
        for (var i = _pendingRemoteBuiOpens.Count - 1; i >= 0; i--)
        {
            var pending = _pendingRemoteBuiOpens[i];
            if (pending.UiEntity != owner
                || pending.UiKey != uiKey
                || pending.RemoteActor != remoteActor)
                continue;

            _pendingRemoteBuiOpens.RemoveAt(i);
            return true;
        }

        return false;
    }

    private void PrunePendingBuiOpens(TimeSpan curTime)
    {
        _pendingBuiWindowOpens.RemoveAll(pending => pending.ExpiresAt <= curTime
                                                    || pending.Window is { Disposed: true });
        _pendingRemoteBuiOpens.RemoveAll(pending => pending.ExpiresAt <= curTime);
    }

    private bool TryEmbedPendingWindow(PendingBuiWindowOpen pendingOpen)
    {
        if (!pendingOpen.RemoteOriginConfirmed
            || pendingOpen.Window is not { } window
            || _uiManager is not { } uiManager)
            return false;

        if (pendingOpen.EmbedScheduled)
            return true;

        pendingOpen.EmbedScheduled = true;
        uiManager.DeferAction(() =>
        {
            if (ControlledEntity == pendingOpen.RemoteEntity)
                TryEmbedWindowInternal(window);
        });
        return true;
    }

    private bool TryEmbedWindowInternal(BaseWindow window)
    {
        if (_window is not { Disposed: false } remoteWindow
            || window.Disposed
            || !window.IsOpen)
            return false;

        remoteWindow.EmbedWindow(window);
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

    public void RequestInteraction(
        EntityUid target,
        bool altInteract,
        bool activateInWorld,
        RemoteControlInteractionAction action = RemoteControlInteractionAction.Interact)
        => InteractionRequested?.Invoke(target, altInteract, activateInWorld, action);

    private sealed class PendingBuiWindowOpen
    {
        public PendingBuiWindowOpen(
            EntityUid owner,
            Enum uiKey,
            EntityUid remoteEntity,
            NetEntity uiEntity,
            string uiKeyIdentifier,
            NetEntity remoteActor,
            TimeSpan expiresAt,
            bool remoteOriginConfirmed)
        {
            Owner = owner;
            UiKey = uiKey;
            RemoteEntity = remoteEntity;
            UiEntity = uiEntity;
            UiKeyIdentifier = uiKeyIdentifier;
            RemoteActor = remoteActor;
            ExpiresAt = expiresAt;
            RemoteOriginConfirmed = remoteOriginConfirmed;
        }

        public EntityUid Owner { get; }
        public Enum UiKey { get; }
        public EntityUid RemoteEntity { get; }
        public NetEntity UiEntity { get; }
        public string UiKeyIdentifier { get; }
        public NetEntity RemoteActor { get; }
        public TimeSpan ExpiresAt { get; }
        public bool RemoteOriginConfirmed { get; set; }
        public bool EmbedScheduled { get; set; }
        public BaseWindow? Window { get; set; }
    }

    private readonly record struct PendingRemoteBuiOpen(
        NetEntity UiEntity,
        string UiKey,
        NetEntity RemoteActor,
        TimeSpan ExpiresAt);
}
