using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Binary.Components;
using GasCanisterComponent = Content.Shared.Atmos.Piping.Unary.Components.GasCanisterComponent;

namespace Content.Server.Atmos.Piping.Unary.EntitySystems;

public sealed partial class GasCanisterSystem
{
    public void RefreshCanister(EntityUid uid, GasCanisterComponent canister)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        DirtyUI(uid, canister);

        canister.LastPressure = canister.Air.Pressure;

        if (canister.Air.Pressure < 10)
        {
            _appearance.SetData(uid, GasCanisterVisuals.PressureState, 0, appearance);
        }
        else if (canister.Air.Pressure < Atmospherics.OneAtmosphere)
        {
            _appearance.SetData(uid, GasCanisterVisuals.PressureState, 1, appearance);
        }
        else if (canister.Air.Pressure < (15 * Atmospherics.OneAtmosphere))
        {
            _appearance.SetData(uid, GasCanisterVisuals.PressureState, 2, appearance);
        }
        else
        {
            _appearance.SetData(uid, GasCanisterVisuals.PressureState, 3, appearance);
        }
    }
}
