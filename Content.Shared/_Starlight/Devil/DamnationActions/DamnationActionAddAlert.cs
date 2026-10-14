using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Devil.DamnationActions;

public sealed partial class DamnationActionAddAlert : DamnationAction
{
    [DataField]
    List<ProtoId<AlertPrototype>> Alerts = new();

    private AlertsSystem _alerts = default!;

    public override bool Action(Entity<DamnedComponent> victim)
    {
        foreach (var alert in Alerts)
        {
            _alerts.ShowAlert(victim.Owner, alert);
        }

        return true;
    }

    public override void ResolveIoC()
    {
        base.ResolveIoC();

        _alerts = _entityManager.System<AlertsSystem>();
    }
}
