using Content.Shared._Starlight.MindCollective;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.MindCollective;

public sealed partial class MindCollectiveUiSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;

    [SubscribeLocalEvent]
    private void OnUiState(Entity<MindCollectiveUiComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_ui.TryGetOpenUi<MindCollectiveBoundUserInterface>(ent.Owner, MindCollectiveUiKey.Key, out var bui))
            bui.Update();
    }
}
