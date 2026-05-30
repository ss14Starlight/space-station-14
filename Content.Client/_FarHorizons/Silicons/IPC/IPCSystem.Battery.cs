using Content.Client._Starlight.Alert;
using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared._Starlight.UI;
using Robust.Shared.Player;

namespace Content.Client._FarHorizons.Silicons.IPC;

public sealed partial class IPCSystem
{
    [Dependency] private readonly BatteryAlertSystem _batteryAlert = default!;

    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private static readonly TimeSpan _updateRate = TimeSpan.FromSeconds(1f);

    protected override void SetupBattery()
    {
        SubscribeLocalEvent<IPCBatteryComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<IPCBatteryComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerDetached(Entity<IPCBatteryComponent> ent, ref LocalPlayerDetachedEvent args) =>
        _alerts.ClearAlert(ent.Owner, ent.Comp.ChargeCritical);

    private void OnPlayerAttached(Entity<IPCBatteryComponent> ent, ref LocalPlayerAttachedEvent args) => UpdateBatteryAlert(ent);

    protected override void UpdateBattery(float frameTime)
    {
        if (_player.LocalEntity is not { } localPlayer)
            return;

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + _updateRate;

        if (TryComp<IPCBatteryComponent>(localPlayer, out var ipcBattery))
            UpdateBatteryAlert((localPlayer, ipcBattery));
    }

    private void UpdateBatteryAlert(Entity<IPCBatteryComponent> ent)
    {
        if (_state.IsAlive(ent) && ent.Comp.TimerActive && !_powerCell.HasDrawCharge(ent.Owner))
            _alerts.ShowAlert(ent.Owner, ent.Comp.ChargeCritical);
        else if (TryComp<BatteryAlertComponent>(ent.Owner, out var battery))
        {
            _alerts.ClearAlert(ent.Owner, ent.Comp.ChargeCritical);
            _batteryAlert.TryUpdateBatteryAlert(ent, battery);
        }
    }
}
