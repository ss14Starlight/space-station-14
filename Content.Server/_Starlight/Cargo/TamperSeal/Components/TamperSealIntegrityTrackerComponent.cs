using Robust.Shared.Audio;

namespace Content.Server._Starlight.Cargo.TamperSeal.Components;

/// <summary>
/// This component tracks tamper seal integrity performance metrics. These metrics are scoped to stations and are
/// server-side only. This component is applied directly to the station entity.
/// </summary>
[RegisterComponent]
public sealed partial class TamperSealIntegrityTrackerComponent : Component
{
    /// <summary>
    /// The ID of the station that this tracking component belongs to.
    /// </summary>
    public EntityUid StationId;

    #region History

    /// <summary>
    /// Historical tamper seal results.
    /// </summary>
    public List<TamperSealResult> Records { get; private set; } = new();

    /// <summary>
    /// The minimum number of records to keep in history. Only affects time-based record expungement.
    /// </summary>
    public int MinRecords = 10;

    /// <summary>
    /// The maximum number of records to keep in history.
    /// </summary>
    public int MaxRecords = 40;

    /// <summary>
    /// The maximum age of records to keep in history. Only affects time-based record expungement.
    /// </summary>
    public TimeSpan RecordLifetime = TimeSpan.FromMinutes(20);

    #endregion
    #region Judgement

    /// <summary>
    /// Whether we're judging delivery performance.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool JudgementEnabled = true;

    /// <summary>
    /// Minimum number of records required before judging.
    /// </summary>
    public int? JudgementMinRecords = 8;

    #endregion
    #region Failure

    /// <summary>
    /// Whether we are currently below the failure threshold, and as such have already sent an announcement.
    /// </summary>
    public bool Failure = false;

    /// <summary>
    /// When the delivery success rate falls below this threshold, we announce and set the failure flag.
    /// </summary>
    public float FailureSetThreshold = .7f;

    /// <summary>
    /// Once the success rate meets or exceeds this threshold, we clear the failure flag.
    /// </summary>
    public float FailureClearThreshold = .8f;

    /// <summary>
    /// The sound to play with the failure announcement.
    /// </summary>
    public SoundSpecifier FailureAnnounceSound = new SoundPathSpecifier("/Audio/Misc/notice1.ogg");

    /// <summary>
    /// The color to use for the failure announcement.
    /// </summary>
    public Color FailureAnnounceColor = Color.Yellow;

    #endregion
}

public sealed class TamperSealResult(TimeSpan time, bool success)
{
    public TimeSpan Time { get; } = time;
    public bool Success { get; } = success;
}
