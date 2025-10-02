using Content.Client.Power;
using Content.Shared.PlayerVendor;
using Content.Shared.UserInterface;

namespace Content.Client.PlayerVendor;

public sealed class PlayerVendorUISystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerVendorComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<PlayerVendorComponent, ActivatableUIOpenAttemptEvent>(OnActivatableUiOpenAttempt, before: new[] { typeof(ActivatableUIRequiresPowerSystem) });
    }

    private void OnAfterState(Entity<PlayerVendorComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!_uiSystem.TryGetOpenUi<PlayerVendorBoundUserInterface>(ent.Owner, PlayerVendorUiKey.Key, out var bui))
            return;
            
        bui.Refresh();
    }

    private void OnActivatableUiOpenAttempt(Entity<PlayerVendorComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.Broken)
            args.Cancel();
    }
}
