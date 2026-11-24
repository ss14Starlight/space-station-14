using Content.Shared._Starlight.UserInterface.Setup;
using Content.Shared.Silicons.StationAi;

namespace Content.Server._Starlight.UserInterface.Setup;

public sealed partial class SetupSystem
{
    [SubscribeLocalEvent]
    private void OnStationAiOpenSetup(EntityUid uid, SetupableComponent component, StationAiOpenSetupEvent args)
    {
        if (!HasComp<StationAiHeldComponent>(args.User)
            || !TryComp<StationAiWhitelistComponent>(uid, out var whitelist)
            || !whitelist.Enabled
            || (component.NameSet && !component.AllowRenaming)
            || !_userInterface.HasUi(uid, SetupUiKey.Key))
            return;

        OpenSetupInterface(uid, args.User, component);
    }
}
