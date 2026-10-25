using Content.Client._Starlight.Genetics.UI;
using Content.Shared._Starlight.Genetics;
using Content.Shared._Starlight.Genetics.Components;

namespace Content.Client._Starlight.Genetics;

public sealed partial class GeneticsConsoleUiSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = null!;

    private readonly HashSet<EntityUid> _pendingRefresh = [];

    /// <summary>
    /// Refreshes the open UI whenever server state is applied. Predicted actions like saving a gene spawn a client-side
    /// gene that gets replaced once the server's arrives, so without this the UI can keep pointing at the deleted one.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnAfterAutoHandleState(Entity<GeneticsConsoleComponent> ent, ref AfterAutoHandleStateEvent args) =>
        _pendingRefresh.Add(ent);

    public override void FrameUpdate(float frameTime)
    {
        foreach (var uid in _pendingRefresh)
        {
            if (Exists(uid) &&
                _ui.TryGetOpenUi<GeneticsConsoleBoundUserInterface>(uid, GeneticsConsoleUiKey.Key, out var bui))
                bui.Refresh();
        }

        _pendingRefresh.Clear();
    }
}
