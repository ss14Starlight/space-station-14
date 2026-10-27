using Content.Client._Starlight.BodyControl;
using Content.Shared._Starlight.BodyControl;
using Content.Shared.Alert;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Client.Alerts;

public sealed partial class ClientAlertsSystem
{
    [Dependency] private BodyControlViewSystem _bodyView = default!;

    private EntityUid? AlertsUiEntity => _bodyView.Entity;

    private bool TryControlAlert(ProtoId<AlertPrototype> alert)
        => _bodyView.Send(new BodyControlCommandEvent(BodyControlCommand.Alert, slot: alert.Id));

    [SubscribeLocalEvent]
    private void OnControlViewChanged(BodyControlViewChangedEvent args)
    {
        ClearAlerts?.Invoke(this, EventArgs.Empty);
        if (ActiveAlerts is { } alerts)
            SyncAlerts?.Invoke(this, alerts);
    }
}
