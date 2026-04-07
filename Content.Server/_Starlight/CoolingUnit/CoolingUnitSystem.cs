using Content.Server.Body.Components;
using Content.Server.Temperature.Systems;
using Content.Shared._Starlight.CoolingUnit;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Temperature.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.CoolingUnit;

public sealed partial class CoolingUnitSystem : SharedCoolingUnitSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TemperatureSystem _tempSys = default!;

    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private readonly TimeSpan _updateCooldown = TimeSpan.FromSeconds(1f);

    public override void Initialize() => base.Initialize();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime > _nextUpdate)
        {
            _nextUpdate = _timing.CurTime + _updateCooldown;

            var query = EntityQueryEnumerator<TemperatureComponent, ThermalRegulatorComponent>();
            while (query.MoveNext(out var uid, out var tempcomponent, out var regulatorcomp))
            {
                if (HasComp<InventoryComponent>(uid) && _inventory.TryGetSlots(uid, out var slots))
                    foreach (var slot in slots)
                        if (_inventory.TryGetSlotEntity(uid, slot.Name, out var slotent) && TryComp<CoolingUnitComponent>(slotent, out var coolingcomp)
                            && TryComp<ItemToggleComponent>(slotent, out var itemtoggle) && itemtoggle.Activated)
                        {
                            if (tempcomponent.CurrentTemperature > regulatorcomp.NormalBodyTemperature)
                            {
                                var coolingAmount = Math.Min(coolingcomp.MaxCooling, tempcomponent.CurrentTemperature - regulatorcomp.NormalBodyTemperature);
                                _tempSys.ChangeHeat(uid, -coolingAmount, true, tempcomponent);
                            }
                        }
            }
        }
    }
}
