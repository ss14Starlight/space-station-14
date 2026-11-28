namespace Content.Server._Starlight.Anomaly.Components;

/// <summary>
/// Provide energy for Brighteye on Pulse & Supercrit.
/// May also provide a chance of a Brighteye spawning on Supercrit.
/// </summary>
[RegisterComponent]
public sealed partial class BrighteyeEnergyAnomalyComponent : Component
{
    [DataField]
    public float PulseRange = 5f;

    /// <summary>
    /// When true, on supercrit has a chance to spawn a Brighteye.
    /// </summary>
    [DataField]
    public bool BrighteyeSpawn = false;
}
