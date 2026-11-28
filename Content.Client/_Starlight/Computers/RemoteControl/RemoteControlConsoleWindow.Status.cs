using Content.Client.Alerts;
using Content.Shared.Alert;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Computers.RemoteControl;

public sealed partial class RemoteControlConsoleWindow
{
    private ClientAlertsSystem _alertsSystem = default!;
    private PowerCellSystem _powerCell = default!;
    private SharedBatterySystem _battery = default!;
    private readonly Dictionary<AlertKey, AlertState> _remoteAlertStates = new();
    private readonly HashSet<AlertKey> _clientHandledBatteryAlerts = new();

    public event Action<ProtoId<AlertPrototype>>? RemoteAlertPressed;

    private void InitializeRemoteStatus()
    {
        _alertsSystem = _entityManager.System<ClientAlertsSystem>();
        _powerCell = _entityManager.System<PowerCellSystem>();
        _battery = _entityManager.System<SharedBatterySystem>();
        RemoteAlerts.AlertPressed += OnRemoteAlertPressed;
    }

    private void OnRemoteAlertPressed(object? sender, ProtoId<AlertPrototype> alert)
        => RemoteAlertPressed?.Invoke(alert);

    public void SetRemoteAlerts(IReadOnlyDictionary<AlertKey, AlertState> alerts)
    {
        _remoteAlertStates.Clear();
        foreach (var (key, state) in alerts)
            _remoteAlertStates[key] = state;

        _clientHandledBatteryAlerts.Clear();
        UpdateRemoteStatus();
    }

    public void UpdateRemoteStatus()
    {
        if (_remoteEntity is { } remoteEntity)
            UpdateRemoteStatus(remoteEntity);
        else
            SyncRemoteAlerts();
    }

    private void UpdateRemoteStatus(EntityUid remoteEntity)
    {
        foreach (var key in _clientHandledBatteryAlerts)
            _remoteAlertStates.Remove(key);

        _clientHandledBatteryAlerts.Clear();
        UpdateBattery(remoteEntity);
        SyncRemoteAlerts();
    }

    private void UpdateBattery(EntityUid remoteEntity)
    {
        var noBatteryAlert = new ProtoId<AlertPrototype>("BorgBatteryNone");
        if (!_powerCell.TryGetBatteryFromSlot(remoteEntity, out var battery))
        {
            if (_alertsSystem.TryGet(noBatteryAlert, out var noBatteryPrototype))
            {
                var key = noBatteryPrototype.AlertKey;
                _remoteAlertStates[key] = new AlertState
                {
                    Type = noBatteryAlert,
                };
                _clientHandledBatteryAlerts.Add(key);
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
        var batteryKey = batteryPrototype.AlertKey;
        _remoteAlertStates[batteryKey] = new AlertState
        {
            Type = batteryAlert,
            Severity = severity,
        };
        _clientHandledBatteryAlerts.Add(batteryKey);
    }

    private void ClearRemoteStatus()
    {
        _remoteAlertStates.Clear();
        _clientHandledBatteryAlerts.Clear();
        SyncRemoteAlerts();
    }

    private void SyncRemoteAlerts()
        => RemoteAlerts.SyncControls(_alertsSystem, _alertsSystem.AlertOrder, _remoteAlertStates);
}
