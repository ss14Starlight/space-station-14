using Content.Shared._Moffstation.Snapshots;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics;

namespace Content.Client._Moffstation.Snapshots;

/// <summary>
/// Shows a Map using the Snapshot recorded on an entity
/// </summary>
public sealed partial class SnapshotViewBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private SnapshotViewWindow? _window;

    [ViewVariables]
    private NetEntity? _sceneSnapshot;

    [ViewVariables]
    public SnapshotData? Snapshot { get; private set; }

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

    public void RequestSnapshot()
        => SendMessage(new SnapshotViewRequestMessage());

    public void SetSnapshot(SnapshotData snapshot)
    {
        if (Snapshot != null || Disposed)
            return;

        Snapshot = snapshot;

        EntMan.System<ClientSnapshotViewSystem>().QueueBuild(this, snapshot);
    }

    public void ShowScene(NetEntity snapshot, IEye eye, Vector2i viewportSize)
    {
        if (Disposed)
        {
            EntMan.System<ClientSnapshotViewSystem>().ReleaseScene(snapshot);
            return;
        }

        _sceneSnapshot = snapshot;

        _window = this.CreateWindow<SnapshotViewWindow>();
        _window.SetScene(eye, viewportSize);
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
