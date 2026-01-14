namespace Content.Server._Starlight.Holograms;

/// <summary>
///     Hologram body scanner that captures appearance and mind data.
///     Outputs both brain and body chips.
/// </summary>
[RegisterComponent]
public sealed partial class HologramBodyScannerComponent : Component
{
    /// <summary>
    ///     Time between scans to prevent spam.
    /// </summary>
    [DataField]
    public TimeSpan ScanDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Last time a scan was performed.
    /// </summary>
    [ViewVariables]
    public TimeSpan LastScanTime = TimeSpan.Zero;
}
