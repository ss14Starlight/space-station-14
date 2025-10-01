using Content.Shared.PlayerVendor;

namespace Content.Client.PlayerVendor;

public sealed class PlayerVendorUISystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerVendorComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnAfterState(Entity<PlayerVendorComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!_uiSystem.TryGetOpenUi<PlayerVendorBoundUserInterface>(ent.Owner, PlayerVendorUiKey.Key, out var bui))
            return;
            
        bui.Refresh();
    }
}
