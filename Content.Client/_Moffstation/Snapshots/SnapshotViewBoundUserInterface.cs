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
    private EntityUid? _sceneMap;

    [ViewVariables]
    public SnapshotData? Snapshot { get; private set; }

    private bool _disposed;

    public bool Disposed => _disposed;

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
    {
        SendMessage(new SnapshotViewRequestMessage());
    }

    public void SetSnapshot(SnapshotData snapshot)
    {
        if (Snapshot != null || _disposed)
            return;

        Snapshot = snapshot;

        EntMan.System<ClientSnapshotViewSystem>().QueueBuild(this, snapshot);
    }

    public void ShowScene(EntityUid map, IEye eye, Vector2i viewportSize)
    {
        if (_disposed)
        {
            EntMan.System<ClientSnapshotViewSystem>().Destroy(map);
            return;
        }

        _sceneMap = map;

        _window = this.CreateWindow<SnapshotViewWindow>();
        _window.SetScene(eye, viewportSize);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _disposed = true;
        if (_sceneMap is { } map)
        {
            _sceneMap = null;
            EntMan.System<ClientSnapshotViewSystem>().Destroy(map);
        }
    }
}
