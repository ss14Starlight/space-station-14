using Robust.Shared.Audio;

namespace Content.Server._Starlight.Cargo.TamperSeal;

[RegisterComponent]
public sealed partial class TamperSealTrackingComponent : Component
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
    public int MaxRecords = 50;

    /// <summary>
    /// The maximum age of records to keep in history. Only affects time-based record expungement
    /// </summary>
    public TimeSpan RecordLifetime = TimeSpan.FromMinutes(20);

    #endregion
    #region Judgement

    /// <summary>
    /// Whether we're judging delivery performance.
    /// </summary>
    public bool JudgementEnabled = false;

    /// <summary>
    /// Minimum number of records required before judging.
    /// </summary>
    public int? JudgementMinRecords = 8;

    /// <summary>
    /// How much
    /// </summary>
    public int JudgementMinTotalValue = 30_000;

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
    ///
    /// </summary>
    public SoundSpecifier FailureAnnounceSound = new SoundPathSpecifier("/Audio/Misc/notice1.ogg");

    /// <summary>
    ///
    /// </summary>
    public Color FailureAnnounceColor = Color.Yellow;

    #endregion
}

public sealed class TamperSealResult(TimeSpan time, bool success, int value)
{
    public TimeSpan Time { get; } = time;
    public bool Success { get; } = success;
    public int Value { get; } = value;
}
