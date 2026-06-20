namespace Content.Server.ArcanusFlux;

/// <summary>
/// Marks this anomaly as an Extreme (overclocked) anomaly.
/// Extreme anomalies generate extra Arcanus Flux per second and can
/// snowball into further events when flux is already high.
/// Added at runtime by <see cref="ArcanusFluxSystem"/> when flux crosses the critical threshold.
/// </summary>
[RegisterComponent]
public sealed partial class ExtremeAnomalyComponent : Component
{
    /// <summary>
    /// Extra Arcanus Flux this anomaly generates per second on top of the base rate.
    /// </summary>
    [DataField]
    public float ExtraFluxPerSecond = 1.5f;

    /// <summary>
    /// Whether this anomaly has been visually marked as Extreme yet.
    /// </summary>
    [DataField]
    public bool MarkedVisually = false;
}
