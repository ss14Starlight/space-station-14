using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;

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
    [ViewVariables(VVAccess.ReadOnly)] public EntityUid StationId;

    /// <summary>
    /// Whether we're judging delivery performance.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)] public bool Enabled = true;

    #region State

    /// <summary>
    /// Historical tamper seal results.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)] public Queue<TamperSealResult> Records = new(40);

    /// <summary>
    /// Minimum number of records required before judging.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)] public int? MinRecords = 8;

    /// <summary>
    /// The maximum number of records to keep in history.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)] public int MaxRecords = 40;

    #endregion
    #region Failure

    /// <summary>
    /// Whether we are currently below the failure threshold, and as such have already sent an announcement.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)] public bool Failure = false;

    /// <summary>
    /// The last recorded success fraction.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), UsedImplicitly]
    public float SuccessFraction;

    /// <summary>
    /// When the delivery success rate falls below this threshold, we announce and set the failure flag.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)] public float FailureSetThreshold = .7f;

    /// <summary>
    /// Once the success rate meets or exceeds this threshold, we clear the failure flag.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)] public float FailureClearThreshold = .8f;

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

[Serializable]
public sealed class TamperSealResult(TimeSpan time, bool success)
{
    public TimeSpan Time { get; } = time;
    public bool Success { get; } = success;
}
