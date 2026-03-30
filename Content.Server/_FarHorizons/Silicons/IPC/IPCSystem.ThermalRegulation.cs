using Content.Shared.Temperature.Components;
using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared.Alert;
using Content.Shared.Atmos;
using Robust.Shared.Prototypes;
using Content.Shared.Inventory;
using Content.Shared._Starlight.CoolingUnit;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Server._FarHorizons.Silicons.IPC;

public sealed partial class IPCSystem
{
    protected override void UpdateThermals(float deltaTime)
    {
        var query = EntityQueryEnumerator<IPCThermalRegulationComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextUpdate ||
               !TryComp<TemperatureComponent>(uid, out var temp) ||
               temp.CurrentTemperature == 0)
                continue;

            comp.NextUpdate = _timing.CurTime + comp.RefreshRate;
            HandleTemperature((uid, comp, temp));
        }
    }

    public void HandleTemperature(Entity<IPCThermalRegulationComponent, TemperatureComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp2) ||
            ExternalCooling(ent, ent.Comp2) ||
            _atmos.GetContainingMixture(ent.Owner) is not { } gas)
            return;

        RadiateHeat(ent, ent.Comp2, gas.Temperature, _state.IsDead(ent) ? 0 : ent.Comp1.ProduceHeat);

        ent.Comp1.FansCurrentlyOff = FanShutOff(ent, gas);

        ent.Comp1.CurrentTemp = ent.Comp2.CurrentTemperature;
        var switchFans = CanSwitchFan(ent);

        if (!ent.Comp1.FansCurrentlyOff)
            HandleFans(ent, ent.Comp2, gas, switchFans);
        else
        {
            ent.Comp1.CanSwitchModeIn = TimeSpan.Zero;
            ent.Comp1.CurrentMode = null;
        }

        Dirty<IPCThermalRegulationComponent>(ent);
        UpdateThermalsAlert(ent);
    }

    private bool ExternalCooling(Entity<IPCThermalRegulationComponent> ent, TemperatureComponent temp)
    {
        if (HasComp<InventoryComponent>(ent) && _inventorySystem.TryGetSlots(ent, out var slots))
            foreach (var slot in slots)
                if (_inventorySystem.TryGetSlotEntity(ent, slot.Name, out var slotent) && HasComp<CoolingUnitComponent>(slotent)
                    && TryComp<ItemToggleComponent>(slotent, out var itemtoggle) && itemtoggle.Activated)
                {
                    ent.Comp.FansCurrentlyOff = false;
                    ent.Comp.CanSwitchModeIn = TimeSpan.Zero;
                    ent.Comp.CurrentTemp = temp.CurrentTemperature;
                    HandleFans(ent, temp, null, true, true);
                    UpdateThermalsAlert(ent);
                    Dirty(ent);
                    return true;
                }

        return false;
    }

    private void RadiateHeat(Entity<IPCThermalRegulationComponent> ent, TemperatureComponent temp, float externalTemp, float produceHeat = 0)
    {
        var tempDelta = temp.CurrentTemperature - externalTemp;

        if (tempDelta > 0)
            _tempSys.ChangeHeat(ent, produceHeat - (tempDelta * ent.Comp.RadiateHeatEfficiency), ignoreHeatResistance: true, temp);
        else if (produceHeat > 0)
            _tempSys.ChangeHeat(ent, produceHeat, ignoreHeatResistance: true, temp);
    }

    private bool FanShutOff(Entity<IPCThermalRegulationComponent> ent, GasMixture gas) =>
        _state.IsDead(ent) ||
        gas.Pressure < ent.Comp.MinPressure ||
        gas.Pressure > ent.Comp.MaxPressure ||
        gas.Temperature > ent.Comp.MaxTemperature;

    private static bool CanSwitchFan(Entity<IPCThermalRegulationComponent> ent)
    {
        if (ent.Comp.CanSwitchModeIn <= TimeSpan.Zero)
            return true;

        ent.Comp.CanSwitchModeIn -= ent.Comp.RefreshRate;
        return false;
    }

    private void HandleFans(Entity<IPCThermalRegulationComponent> ent, TemperatureComponent temp, GasMixture? gas, bool canSwitch = false, bool isolated = false)
    {
        if (gas == null && !isolated)
            return;

        float TotalEfficiency = 1;
        if (!isolated)
        {
            float PressureEfficiency = 1;
            if (gas!.Pressure < ent.Comp.MinEffectivePressure)
                PressureEfficiency = (gas!.Pressure - ent.Comp.MinPressure) / (ent.Comp.MinEffectivePressure - ent.Comp.MinPressure);
            else if (gas!.Pressure > ent.Comp.MaxEffectivePressure)
                PressureEfficiency = 1 - ((gas!.Pressure - ent.Comp.MaxEffectivePressure) / (ent.Comp.MaxPressure - ent.Comp.MaxEffectivePressure));

            float TemperatureEfficiency = 1;
            if (gas!.Temperature > temp.CurrentTemperature)
                TemperatureEfficiency = 1 - (gas!.Temperature - temp.CurrentTemperature) / (ent.Comp.MaxTemperature - temp.CurrentTemperature);

            TotalEfficiency = PressureEfficiency * TemperatureEfficiency;
        }

        ent.Comp.CurrentEfficiency = TotalEfficiency;

        if (canSwitch)
        {
            ent.Comp.CurrentMode = null;

            foreach (var mode in ent.Comp.OrderedFanModes)
            {
                if (temp.CurrentTemperature >= mode.MinTemp)
                {
                    ent.Comp.CurrentMode = mode;
                    ent.Comp.CanSwitchModeIn = mode.StaysOnFor;
                    break;
                }
            }
        }

        if (ent.Comp.CurrentMode != null)
        {
            if (!isolated)
                _atmos.AddHeat(gas!, ent.Comp.CurrentMode.AtmosHeatEffect * TotalEfficiency);
            _tempSys.ChangeHeat(ent, ent.Comp.CurrentMode.BodyHeatEffect * TotalEfficiency, true);
        }
    }

    private void UpdateThermalsAlert(Entity<IPCThermalRegulationComponent> ent)
    {
        var newAlert = ent.Comp.FansCurrentlyOff
            ? ent.Comp.FansOffAlert
            : ent.Comp.CurrentEfficiency < ent.Comp.FansEfficiencyLowThreshold
            ? ent.Comp.FansEfficiencyLowAlert
            : ent.Comp.CurrentMode?.ModeAlert is ProtoId<AlertPrototype> alert ? alert : ent.Comp.FansOKAlert;

        if (newAlert != ent.Comp.CurrentAlert)
        {
            _alerts.ClearAlertCategory(ent.Owner, ent.Comp.AlertsCategory);
            _alerts.ShowAlert(ent.Owner, newAlert);
            ent.Comp.CurrentAlert = newAlert;
        }
    }
}
