using Content.Server.Anomaly;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Anomaly.Components;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Pins tutorial anomaly start values after the base anomaly MapInit roll.
/// </summary>
public sealed partial class TutorialAnomalySystem : EntitySystem
{
    private const float TargetStability = 0.55f;
    private const float TargetSeverity = 0.15f;

    [Dependency] private AnomalySystem _anomaly = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialAnomalyComponent, MapInitEvent>(OnMapInit, after: [typeof(AnomalySystem)]);
    }

    private void OnMapInit(Entity<TutorialAnomalyComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<AnomalyComponent>(ent, out var anomaly))
            return;

        _anomaly.ChangeAnomalyStability(ent, TargetStability - anomaly.Stability, anomaly);
        _anomaly.ChangeAnomalySeverity(ent, TargetSeverity - anomaly.Severity, anomaly);
    }
}
