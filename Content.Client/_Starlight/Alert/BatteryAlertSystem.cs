using Content.Shared._Starlight.UI;
using Content.Shared.Alert;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Alert;

public sealed partial class BatteryAlertSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;

    // How often to update the battery alert.
    // Also gets updated instantly when switching bodies or a battery is inserted or removed.
    private static readonly TimeSpan _alertUpdateDelay = TimeSpan.FromSeconds(1f);

    // Don't put this on the component because we only need to track the time for a single entity
    // and we don't want to TryComp it every single tick.
    private TimeSpan _nextAlertUpdate = TimeSpan.Zero;
    private EntityQuery<BatteryAlertComponent> _alertQuery;
    private EntityQuery<PowerCellSlotComponent> _slotQuery;

    public override void Initialize()
    {
        SubscribeLocalEvent<BatteryAlertComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<BatteryAlertComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<BatteryAlertComponent, PowerCellChangedEvent>(OnPowerCellChanged);

        _alertQuery = GetEntityQuery<BatteryAlertComponent>();
        _slotQuery = GetEntityQuery<PowerCellSlotComponent>();
    }

    private void OnPlayerAttached(EntityUid uid, BatteryAlertComponent component, LocalPlayerAttachedEvent args)
        => TryUpdateBatteryAlert(uid, component);

    private void OnPlayerDetached(Entity<BatteryAlertComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        // Remove all borg related alerts.
        _alerts.ClearAlert(ent.Owner, ent.Comp.BatteryAlert);
        _alerts.ClearAlert(ent.Owner, ent.Comp.NoBatteryAlert);
    }

    private void OnPowerCellChanged(EntityUid uid, BatteryAlertComponent component, PowerCellChangedEvent args)
        => TryUpdateBatteryAlert(uid, component);

    public bool TryUpdateBatteryAlert(EntityUid uid, BatteryAlertComponent? comp = null, PowerCellSlotComponent? slotComponent = null)
    {
        if (!Resolve(uid, ref comp, ref slotComponent, false))
            return false;

        if (!_powerCell.TryGetBatteryFromSlot((uid, slotComponent), out var battery))
        {
            _alerts.ClearAlert(uid, comp.BatteryAlert);
            _alerts.ShowAlert(uid, comp.NoBatteryAlert);
            return true;
        }

        // Alert levels from 0 to 10.
        var chargePercent = (short)MathF.Round(_battery.GetChargeLevel(battery.Value.AsNullable()) * 10f);

        // we make sure 0 only shows if they have absolutely no battery.
        // also account for floating point imprecision
        if (chargePercent == 0 && _powerCell.HasDrawCharge((uid, null, slotComponent)))
            chargePercent = 1;

        _alerts.ClearAlert(uid, comp.NoBatteryAlert);
        _alerts.ShowAlert(uid, comp.BatteryAlert, chargePercent);
        return true;
    }

    // Periodically update the charge indicator.
    // We do this with a client-side alert so that we don't have to network the charge level.
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalEntity is not { } localPlayer)
            return;

        var curTime = _timing.CurTime;

        if (curTime < _nextAlertUpdate)
            return;

        _nextAlertUpdate = curTime + _alertUpdateDelay;

        if (!_alertQuery.TryComp(localPlayer, out var alert) || !_slotQuery.TryComp(localPlayer, out var slot))
            return;

        TryUpdateBatteryAlert(localPlayer, alert, slot);
    }
}
