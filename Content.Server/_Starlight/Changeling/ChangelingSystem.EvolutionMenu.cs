using Content.Shared._Starlight.Changeling;
using Content.Shared.Store;
using Content.Shared.Store.Components;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private SharedUserInterfaceSystem _evolutionUi = default!;

    /// <summary>
    /// Toggles the changeling's own evolution store while allowing the self-targeted action during incapacitation.
    /// </summary>
    /// <remarks>
    /// The input-validation exemption applies only to opening or closing the interface. Its original setting is
    /// restored even if toggling fails, so subsequent UI messages retain the store's normal validation.
    /// </remarks>
    [SubscribeLocalEvent]
    private void OnOpenEvolutionMenu(EntityUid uid, ChangelingComponent comp, ref OpenEvolutionMenuEvent args)
    {
        if (!TryComp<StoreComponent>(uid, out var store)
            || !_evolutionUi.TryGetInterfaceData(uid, StoreUiKey.Key, out var data))
            return;

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
