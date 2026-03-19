using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Robust.Shared.Player;

namespace Content.Client._FarHorizons.Silicons.IPC;

public sealed partial class IPCSystem
{
    [ViewVariables]
    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private static readonly TimeSpan UpdateRate = TimeSpan.FromSeconds(1f);

    protected override void SetupBattery()
    {
        SubscribeLocalEvent<IPCBatteryComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<IPCBatteryComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerDetached(Entity<IPCBatteryComponent> ent, ref LocalPlayerDetachedEvent args) =>
        _alerts.ClearAlertCategory(ent.Owner, ent.Comp.BatteryAlertsCategory);
    private void OnPlayerAttached(Entity<IPCBatteryComponent> ent, ref LocalPlayerAttachedEvent args) => UpdateBatteryAlert(ent);

    protected override void UpdateBattery(float frameTime) 
    {
        if (_player.LocalEntity is not { } localPlayer)
            return;
        
        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateRate;

        if (TryComp<IPCBatteryComponent>(localPlayer, out var ipcBattery))
            UpdateBatteryAlert((localPlayer, ipcBattery));
    }

    private void UpdateBatteryAlert(Entity<IPCBatteryComponent> ent)
    {
        if (_state.IsAlive(ent) && ent.Comp.TimerActive && !_powerCell.HasDrawCharge(ent.Owner)){
            _alerts.ShowAlert(ent.Owner, ent.Comp.ChargeCritical);
            return;
        }

        if (!_powerCell.TryGetBatteryFromSlot((ent, ent.Comp.PowerCellSlot), out var battery))
        {
            _alerts.ShowAlert(ent.Owner, ent.Comp.NoBatteryAlert);
            return;
        }

        var chargePercent = (short) MathF.Round(_battery.GetChargeLevel((battery.Value.Owner, battery.Value.Comp)) * 10f);
        _alerts.ShowAlert(ent.Owner, ent.Comp.BatteryAlert, chargePercent);
    }

}