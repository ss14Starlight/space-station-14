using Content.Shared._Starlight.Snapshots;
using Content.Shared.Popups;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics;

namespace Content.Client._Starlight.Snapshots;

/// <summary>
/// Shows a client-built map for the snapshot recorded on an entity. Owns the window and releases the cached scene on close.
/// </summary>
public sealed partial class SnapshotViewBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private NetEntity? _sceneSnapshot;

    [ViewVariables]
    public SnapshotData? Snapshot { get; private set; }

    /// <summary>
    /// Whether this UI has been disposed. Set once and never cleared; late callbacks check it before building scenes.
    /// </summary>
    public bool Disposed { get; private set; }

    protected override void Open()
    {
        base.Open();

        var view = EntMan.System<ClientSnapshotViewSystem>();
        if (view.TryGetKnownSnapshot(Owner, out var known))
        {
            SetSnapshot(known);
            return;
        }

        RequestSnapshot();
        view.AwaitSnapshot(this);
    }

    /// <summary>
    /// Asks the server for this view's snapshot data.
    /// </summary>
    public void RequestSnapshot()
        => SendMessage(new SnapshotViewRequestMessage());

    /// <summary>
    /// Stores the first snapshot and queues the map build. Later calls, and calls after disposal, are ignored.
    /// </summary>

    public void SetSnapshot(SnapshotData snapshot)
    {
        if (Snapshot != null || Disposed)
            return;

        Snapshot = snapshot;

        EntMan.System<ClientSnapshotViewSystem>().QueueBuild(this, snapshot);
    }

    /// <summary>
    /// Shows a visible failure message and closes this UI when the server never delivered a snapshot.
    /// </summary>
    public void ShowSnapshotFailure()
    {
        if (PlayerManager.LocalEntity is { } localEntity)
        {
            EntMan.System<SharedPopupSystem>().PopupClient(
                Loc.GetString("snapshot-capture-failed"),
                localEntity,
                localEntity);
        }

        Close();
    }

    /// <summary>
    /// Opens the view window for a built scene. If this UI is already disposed, releases the scene instead.
    /// </summary>
    public void ShowScene(NetEntity snapshot, IEye eye, Vector2i viewportSize)
    {
        if (Disposed)
        {
            EntMan.System<ClientSnapshotViewSystem>().ReleaseScene(snapshot);
            return;
        }

        _sceneSnapshot = snapshot;

        var window = this.CreateWindow<SnapshotViewWindow>();
        window.SetScene(eye, viewportSize);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        Disposed = true;
        if (_sceneSnapshot is { } snapshot)
        {
            _sceneSnapshot = null;
            EntMan.System<ClientSnapshotViewSystem>().ReleaseScene(snapshot);
        }
    }
}
