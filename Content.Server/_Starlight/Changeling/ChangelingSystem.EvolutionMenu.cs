using Content.Shared._Starlight.Changeling;
using Content.Shared.Store;
using Content.Shared.Store.Components;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private SharedUserInterfaceSystem _evolutionUi = default!;

    [SubscribeLocalEvent]
    private void OnOpenEvolutionMenu(EntityUid uid, ChangelingComponent comp, ref OpenEvolutionMenuEvent args)
    {
        if (!TryComp<StoreComponent>(uid, out var store)
            || !_evolutionUi.TryGetInterfaceData(uid, StoreUiKey.Key, out var data))
            return;

        // The self-only action allows opening while unconscious. Keep normal validation for later UI messages.
        var validate = data.RequireInputValidation;
        data.RequireInputValidation = false;
        try
        {
            _store.ToggleUi(uid, uid, store);
        }
        finally
        {
            data.RequireInputValidation = validate;
        }

        args.Handled = true;
    }
}
