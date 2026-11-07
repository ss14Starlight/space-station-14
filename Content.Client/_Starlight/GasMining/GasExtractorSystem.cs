using Content.Shared._Starlight.GasMining.GasExtractor;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.GasMining;

public sealed partial class GasExtractorSystem : SharedGasExtractorSystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;

    protected override void UpdateUi(Entity<GasExtractorComponent> entity)
    {
        if (_ui.TryGetOpenUi(entity.Owner, GasExtractorUiKey.Key, out var bui))
        {
            bui.Update();
        }
    }

    [SubscribeLocalEvent]
    private void OnExtractorState(Entity<GasExtractorComponent> entity, ref AfterAutoHandleStateEvent _) => UpdateUi(entity);
}
