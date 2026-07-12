using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;

namespace Content.Shared._Starlight.Power.EntitySystems;

public sealed partial class BatterySelfRechargerSystem : EntitySystem
{
    [Dependency] private SharedBatterySystem _battery = default!;

    public void UpdateAutoRechargeRate(EntityUid uid, float value, BatterySelfRechargerComponent? comp)
    {
        if (!Resolve(uid, ref comp)) return;

        comp.AutoRechargeRate = value;
        Dirty(uid, comp);
        _battery.RefreshChargeRate(uid);
    }
}
