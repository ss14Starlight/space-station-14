using Content.Client._Starlight.BodyControl;
using System.Linq;
using Content.Shared.Alert;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Alerts;

[UsedImplicitly]
public sealed partial class ClientAlertsSystem : AlertsSystem
{
    public AlertOrderPrototype? AlertOrder { get; set; }

    [Dependency] private BodyControlViewSystem _bodyView = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    public event EventHandler? ClearAlerts;
    public event EventHandler<IReadOnlyDictionary<AlertKey, AlertState>>? SyncAlerts;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlertsComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<AlertsComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<AlertsComponent, ComponentHandleState>(OnHandleState);
    }

    protected override void HandledAlert()
    {
        _ui.ClickSound();
    }

    protected override void LoadPrototypes()
    {
        base.LoadPrototypes();

        AlertOrder = _prototypeManager.EnumeratePrototypes<AlertOrderPrototype>().FirstOrDefault();
        if (AlertOrder == null)
            Log.Error("No alertOrder prototype found, alerts will be in random order");
    }

    public IReadOnlyDictionary<AlertKey, AlertState>? ActiveAlerts
    {
        get
        {
            var ent = _bodyView.Entity;
            return ent is not null
                ? GetActiveAlerts(ent.Value)
                : null;
        }
    }

    private void OnHandleState(Entity<AlertsComponent> alerts, ref ComponentHandleState args)
    {
        if (args.Current is not AlertComponentState cast)
            return;

        // Save all client-sided alerts to later put back in
        var clientAlerts = new Dictionary<AlertKey, AlertState>();
        foreach (var alert in alerts.Comp.Alerts)
        {
            if (alert.Key.AlertType != null && TryGet(alert.Key.AlertType.Value, out var alertProto))
            {
                if (alertProto.ClientHandled)
                    clientAlerts[alert.Key] = alert.Value;
            }
        }

        alerts.Comp.Alerts = new(cast.Alerts);

        foreach (var alert in clientAlerts)
        {
            alerts.Comp.Alerts[alert.Key] = alert.Value;
        }

        UpdateHud(alerts);
    }

    protected override void AfterShowAlert(Entity<AlertsComponent> alerts)
    {
        UpdateHud(alerts);
    }

    protected override void AfterClearAlert(Entity<AlertsComponent> alerts)
    {
        UpdateHud(alerts);
    }

    private void UpdateHud(Entity<AlertsComponent> entity)
    {
        if (_bodyView.Entity == entity.Owner)
            SyncAlerts?.Invoke(this, entity.Comp.Alerts);
    }

    private void OnPlayerAttached(EntityUid uid, AlertsComponent component, LocalPlayerAttachedEvent args)
    {
        if (_bodyView.Entity != uid)
            return;

        SyncAlerts?.Invoke(this, component.Alerts);
    }

    protected override void HandleComponentShutdown(EntityUid uid, AlertsComponent component, ComponentShutdown args)
    {
        base.HandleComponentShutdown(uid, component, args);

        if (_bodyView.Entity != uid)
            return;

        ClearAlerts?.Invoke(this, EventArgs.Empty);
    }

    private void OnPlayerDetached(EntityUid uid, AlertsComponent component, LocalPlayerDetachedEvent args)
    {
        ClearAlerts?.Invoke(this, EventArgs.Empty);
    }

    public void AlertClicked(ProtoId<AlertPrototype> alertType)
    {
        if (_bodyView.Send(new Content.Shared._Starlight.BodyControl.BodyControlCommandEvent(
                Content.Shared._Starlight.BodyControl.BodyControlCommand.Alert, slot: alertType.Id)))
            return;
        RaisePredictiveEvent(new ClickAlertEvent(alertType));
    }

    [SubscribeLocalEvent]
    private void OnControlViewChanged(BodyControlViewChangedEvent args)
    {
        ClearAlerts?.Invoke(this, EventArgs.Empty);
        if (ActiveAlerts is { } alerts)
            SyncAlerts?.Invoke(this, alerts);
    }
}
