namespace Content.Server._Starlight.Anomaly.Components;

/// <summary>
/// Anomaly will break Lights around it on Pulse & Supercrit.
/// </summary>
[RegisterComponent]
public sealed partial class BreakLightAnomalyComponent : Component
{
    [DataField]
    public float PulseRange = 5f;
}
