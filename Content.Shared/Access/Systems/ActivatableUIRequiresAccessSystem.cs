using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared.Access.Components;
using Content.Shared.Lock;

namespace Content.Shared.Access.Systems;
public sealed partial class ActivatableUIRequiresAccessSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActivatableUIRequiresAccessComponent, ActivatableUIOpenAttemptEvent>(OnUIOpenAttempt);
    }

    private void OnUIOpenAttempt(Entity<ActivatableUIRequiresAccessComponent> activatableUI, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;
        // Starlight-start
        if (TryComp<LockComponent>(activatableUI, out var lockComponent)
            && activatableUI.Comp.AllowUnlocking
            && !lockComponent.Locked)
            return;
        // Starlight-end
        if (!_access.IsAllowed(args.User, activatableUI))
        {
            args.Cancel();
            if (activatableUI.Comp.PopupMessage != null && !args.Silent)
                _popup.PopupClient(Loc.GetString(activatableUI.Comp.PopupMessage), activatableUI, args.User);
        }
    }
}

