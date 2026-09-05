using Content.Server._Starlight.StationEvents.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.StationEvents.Components;


/// 
/// 

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