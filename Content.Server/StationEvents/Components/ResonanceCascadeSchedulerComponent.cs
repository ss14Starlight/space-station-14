using Content.Server.StationEvents;
using Robust.Shared.Prototypes;

namespace Content.Server.StationEvents.Components;


///  Periodically will spawn anomalies from a the set of possible anomalies listed in meteorswarms.yml. The interval between spawns will decrease over time, and the anomalies will be spawned at random locations in station.


[RegisterComponent, Access(typeof(ResonanceCascadeSchedulerSystem))]
public sealed partial class ResonanceCascadeSchedulerComponent : Component
{
    [DataField]
    public EntProtoId SelectedAnomalyPrototype = string.Empty;

    [DataField]
    public List<EntProtoId> PossibleAnomalyPrototypes = new()
    {
        "AnomalyClown",
        "AnomalyFlesh",
        "AnomalyFlora"
    };

    [DataField]
    public TimeSpan StartedAt;

    [DataField]
    public TimeSpan NextSpawnAt;

    [DataField]
    public float InitialDelay = 300f;

    [DataField]
    public float BaseInterval = 240f;

    [DataField]
    public float MinimumInterval = 45f;

    [DataField]
    public float EscalationStep = 300f;

    [DataField]
    public float EscalationMultiplier = 0.9f;
}
