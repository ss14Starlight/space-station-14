using Content.Client.Alerts;
using Content.Client.UserInterface.Systems.Alerts.Widgets;
using Content.Shared.Alert;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Computers.RemoteControl;

public sealed partial class RemoteControlConsoleWindow
{
    private ClientAlertsSystem _alertsSystem = default!;
    private DamageableSystem _damageable = default!;
    private MobThresholdSystem _mobThreshold = default!;
    private PowerCellSystem _powerCell = default!;
    private SharedBatterySystem _battery = default!;
    private readonly Dictionary<AlertKey, AlertState> _remoteAlertStates = new();

    private void InitializeRemoteStatus()
    {
        _alertsSystem = _entityManager.System<ClientAlertsSystem>();
        _damageable = _entityManager.System<DamageableSystem>();
        _mobThreshold = _entityManager.System<MobThresholdSystem>();
        _powerCell = _entityManager.System<PowerCellSystem>();
        _battery = _entityManager.System<SharedBatterySystem>();
    }

    public void UpdateRemoteStatus()
    {
        if (_remoteEntity is not { } remoteEntity)
            return;

        UpdateRemoteStatus(remoteEntity);
    }

    private void UpdateRemoteStatus(EntityUid remoteEntity)
    {
        _remoteAlertStates.Clear();
        UpdateHealth(remoteEntity);
        UpdateBattery(remoteEntity);
        RemoteAlerts.SyncControls(_alertsSystem, _alertsSystem.AlertOrder, _remoteAlertStates);
    }

    private void UpdateHealth(EntityUid remoteEntity)
    {
        if (!_entityManager.TryGetComponent(remoteEntity, out DamageableComponent? damageable)
            || !_entityManager.TryGetComponent(remoteEntity, out MobThresholdsComponent? thresholds)
            || !_entityManager.TryGetComponent(remoteEntity, out MobStateComponent? mobState)
            || !_mobThreshold.TryGetStateAlert(remoteEntity, mobState.CurrentState, out var healthAlert, thresholds)
            || !_alertsSystem.TryGet(healthAlert, out var healthPrototype))
            return;

        short? severity = null;
        if (healthPrototype.SupportsSeverity)
        {
            var hasMaximumDamage = _mobThreshold.TryGetThresholdForState(remoteEntity, MobState.Dead,
                    out var maximumDamage, thresholds)
                || _mobThreshold.TryGetThresholdForState(remoteEntity, MobState.Critical,
                    out maximumDamage, thresholds);
            var damageRatio = hasMaximumDamage && maximumDamage > 0
                ? Math.Clamp((_damageable.GetTotalDamage((remoteEntity, damageable)) / maximumDamage.Value).Float(), 0f, 1f)
                : 0f;
            severity = (short)MathF.Round(MathHelper.Lerp(
                healthPrototype.MinSeverity,
                healthPrototype.MaxSeverity,
                damageRatio));
        }

        _remoteAlertStates[healthPrototype.AlertKey] = new AlertState
        {
            Type = healthAlert,
            Severity = severity,
        };
    }

    private void UpdateBattery(EntityUid remoteEntity)
    {
        var noBatteryAlert = new ProtoId<AlertPrototype>("BorgBatteryNone");

        if (!_powerCell.TryGetBatteryFromSlot(remoteEntity, out var battery))
        {
            if (_alertsSystem.TryGet(noBatteryAlert, out var noBatteryPrototype))
            {
                _remoteAlertStates[noBatteryPrototype.AlertKey] = new AlertState
                {
                    Type = noBatteryAlert,
                };
            }

            return;
        }

        if (_alertsSystem.TryGet(noBatteryAlert, out var withBatteryPrototype))
            _remoteAlertStates.Remove(withBatteryPrototype.AlertKey);

        var batteryAlert = new ProtoId<AlertPrototype>("BorgBattery");
        if (!_alertsSystem.TryGet(batteryAlert, out var batteryPrototype))
            return;

        var severity = (short)MathF.Round(
            Math.Clamp(_battery.GetChargeLevel(battery.Value.AsNullable()), 0f, 1f)
            * batteryPrototype.MaxSeverity);
        _remoteAlertStates[batteryPrototype.AlertKey] = new AlertState
        {
            Type = batteryAlert,
            Severity = severity,
        };
    }

    private void ClearRemoteStatus()
    {
        _remoteAlertStates.Clear();
        RemoteAlerts.SyncControls(_alertsSystem, _alertsSystem.AlertOrder, _remoteAlertStates);
    }
}
