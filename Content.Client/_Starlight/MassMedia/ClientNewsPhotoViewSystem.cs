using Content.Client._Starlight.Snapshots;
using Content.Shared.MassMedia.Systems;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.MassMedia;

/// <summary>
/// Shows a news article photograph (single per article) in a snapshot viewer window.
/// Server only sends data on explicit PDA request, so this never broadcasts photo data.
/// </summary>
public sealed partial class ClientNewsPhotoViewSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private ClientSnapshotViewSystem _snapshotView = default!;

    private SnapshotViewWindow? _window;
    private EntityUid? _map;

    [SubscribeNetworkEvent]
    private void OnPhotoData(NewsArticlePhotoDataEvent ev)
    {
        CloseWindow();

        var (map, eye, size) = _snapshotView.BuildScene(ev.Data);
        _map = map;
        _window = _ui.CreateWindow<SnapshotViewWindow>();
        _window.OnClose += CloseWindow;
        _window.SetScene(eye, size);
        _window.OpenCentered();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CloseWindow();
    }

    private void CloseWindow()
    {
        if (_window is not null)
        {
            _window.OnClose -= CloseWindow;
            _window.Close();
            _window = null;
        }

        if (_map is { } map)
        {
            _snapshotView.Destroy(map);
            _map = null;
        }
    }
}
