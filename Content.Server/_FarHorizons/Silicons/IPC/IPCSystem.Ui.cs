// IPC System - UI (Server)
// SOURCE: Far-Horizons-SS14
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135
// _STARLIGHT: Namespace changes for compatibility

using Content.Shared._FarHorizons.Silicons.IPC;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Server.GameObjects;

namespace Content.Server._FarHorizons.Silicons.IPC;

public sealed partial class IPCSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    
    public void UpdateUI(EntityUid uid)
    {
        var chargePercent = 0f;
        var hasBattery = false;
        var mobState = MobState.Dead;
        if (_powerCell.TryGetBatteryFromSlot(uid, out var battery))
        {
            hasBattery = true;
            var batteryEnt = battery!.Value;
            chargePercent = _predictedBattery.GetChargeLevel((batteryEnt.Owner, batteryEnt.Comp));
        }

        if (TryComp<MobStateComponent>(uid, out var mobStateComp))
            mobState = mobStateComp.CurrentState;
        
        _ui.SetUiState(uid, IPCUiKey.Key,
            new IPCBuiState(chargePercent, hasBattery, mobState));
    }
}

