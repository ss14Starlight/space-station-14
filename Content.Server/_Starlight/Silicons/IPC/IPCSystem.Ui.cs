// IPC System - UI (Server)
// Created by Killer Tamashi and Princess Gurchi for the FH project.
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135

using Content.Shared._Starlight.Silicons.IPC;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Server.GameObjects;

namespace Content.Server._Starlight.Silicons.IPC;

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
            chargePercent = _battery.GetChargeLevel((batteryEnt.Owner, batteryEnt.Comp));
        }

        if (TryComp<MobStateComponent>(uid, out var mobStateComp))
            mobState = mobStateComp.CurrentState;
        
        _ui.SetUiState(uid, IPCUiKey.Key,
            new IPCBuiState(chargePercent, hasBattery, mobState));
    }
}

