using Content.Shared.Verbs;

namespace Content.Shared._Starlight.Medical.CrewMonitoring;

/// <summary>
///     Verbs for toggling alerts on and off. Predicted.
/// </summary>
public sealed class CrewMonitorAlertsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrewMonitorAlertsComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
    }

    private void OnGetAlternativeVerbs(Entity<CrewMonitorAlertsComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var enabled = ent.Comp.Enabled;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(enabled ? "crew-monitoring-verb-alerts-disable" : "crew-monitoring-verb-alerts-enable"),
            Act = () =>
            {
                ent.Comp.Enabled = !ent.Comp.Enabled;
                Dirty(ent);
            },
            Priority = -1,
        });
    }
}
