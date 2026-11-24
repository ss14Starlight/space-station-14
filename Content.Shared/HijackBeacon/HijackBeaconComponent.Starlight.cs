namespace Content.Shared.HijackBeacon;

public sealed partial class HijackBeaconComponent
{
    /// <summary>
    ///     What percentage of funds should be withdrawn from each department account?
    ///     The total value will always be at least the amount of the Fine field.
    /// </summary>
    [DataField] public double FinePercentage = 0.85;
}