using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Starlight.Silicons.Borgs;

/// <summary>
/// Samples a borg's position so that a robotics console can report where it used to be.
/// </summary>
[RegisterComponent, Access(typeof(BorgLocationTrackerSystem))]
public sealed partial class BorgLocationTrackerComponent : Component
{
    /// <summary>
    /// How long between samples, which is also how far behind <see cref="ReportedLocation"/> lags.
    /// </summary>
    [DataField]
    public TimeSpan SampleDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// When to next take a sample.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextSample = TimeSpan.Zero;

    /// <summary>
    /// The most recent sample, which becomes <see cref="ReportedLocation"/> at the next one.
    /// </summary>
    [DataField]
    public string PendingLocation = string.Empty;

    /// <summary>
    /// The sample robotics consoles are told about, taken <see cref="SampleDelay"/> before it was published.
    /// </summary>
    [DataField]
    public string ReportedLocation = string.Empty;
}
