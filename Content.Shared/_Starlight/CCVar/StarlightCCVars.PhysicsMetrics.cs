using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Seconds between awake-body samples exported to Prometheus as <c>physics_awake_*</c>. 0 disables them.
    /// Each sample is spread over several ticks. Sampling is skipped while <c>metrics.enabled</c> is off.
    /// </summary>
    public static readonly CVarDef<float> PhysicsAwakeMetricsInterval =
        CVarDef.Create("physics.awake_metrics_interval", 30f, CVar.SERVERONLY);
}
